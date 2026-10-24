using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.CleanRoom;
using Client.Game.InGame.Context;
using Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom;
using Client.WebUiHost.Game.Topics.BlockDetail.NetworkInfo;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 開いているブロックのネット集約情報をキャッシュ
    /// - electric/gear/cleanRoom: 汎用ポーラー BlockInfoPoller の1秒ポーリング
    /// - filter: 開時1回取得
    /// Caches network aggregates for the open block
    /// - electric/gear/cleanRoom: 1s polling through the generic BlockInfoPoller
    /// - filter: fetched once on open
    /// </summary>
    public class BlockNetworkInfoCache
    {
        // 網に属さない・未取得なら null。通信失敗ではポーラーが前回値を保つ
        // Null when not a member or not fetched yet; on a transport failure the poller keeps the last value
        public GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot Electric => _electricPoller.TryGetSnapshot(out var info) ? info : null;
        public GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot GearNetwork => _gearPoller.TryGetSnapshot(out var info) ? info : null;
        public FilterSplitterStateProtocol.FilterSplitterStateResponse FilterSplitter { get; private set; }

        // 歯車網を追っているか。Topic の連続サンプリングはこれを読み、判定はここだけで行う
        // Whether the gear network is tracked; the topic's continuous sampling reads this, so the check lives only here
        public bool IsTrackingGear { get; private set; }

        // 部屋欄をどの口で組むか。判定は ToCleanRoomTarget の switch だけで行い、組み立て側はこれを読む
        // Which entry point builds the room section; only the switch in ToCleanRoomTarget decides it and the builder reads it
        public CleanRoomDetailTarget CleanRoomTarget { get; private set; }

        // どれかの集約情報が取得済み・消失に変わったときに発火する（Topic が再配信する）
        // Fires when any aggregate becomes fetched or vanished so the topic republishes
        public IObservable<Unit> OnUpdated => _onUpdated;

        private readonly Subject<Unit> _onUpdated = new();
        private readonly BlockInfoPoller<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack, GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot> _electricPoller = ElectricNetworkInfoPollSource.CreatePoller();
        private readonly BlockInfoPoller<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack, GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot> _gearPoller = GearNetworkInfoPollSource.CreatePoller();
        private readonly BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView> _cleanRoomPoller = CleanRoomInfoPollSource.CreatePoller();
        private CancellationTokenSource _cts;
        private Vector3Int _currentPos = new(int.MinValue, int.MinValue, int.MinValue);

        public BlockNetworkInfoCache()
        {
            // どのポーラーが取得済み・消失に変わっても再配信する（通信失敗では発火しない）
            // Republish whenever any poller becomes fetched or vanished; transport failures never fire
            _electricPoller.OnUpdated.Subscribe(_ => _onUpdated.OnNext(Unit.Default));
            _gearPoller.OnUpdated.Subscribe(_ => _onUpdated.OnNext(Unit.Default));
            _cleanRoomPoller.OnUpdated.Subscribe(_ => _onUpdated.OnNext(Unit.Default));
        }

        // 取得済みのときだけ部屋の状態を返す。未取得と消失はどちらも false に畳む（どの読み手も欄を出さない扱いで同じため。消失の警告はポーラーが出す）
        // Returns the room state only while fetched; not fetched and vanished both fold into false since every reader omits the section either way (the poller warns on vanishing)
        public bool TryGetCleanRoom(out CleanRoomInfoView view)
        {
            return _cleanRoomPoller.TryGetSnapshot(out view);
        }

        // 開いたブロックに合わせて取得を開始する。ブロックが変わったら前の取得を打ち切る
        // Start fetching for the opened block; cancel previous fetches when the block changes
        public void Track(BlockGameObject block)
        {
            if (block != null && block.BlockPosInfo.OriginalPos == _currentPos && _cts != null) return;
            Clear();
            if (block == null) return;
            _currentPos = block.BlockPosInfo.OriginalPos;
            _cts = new CancellationTokenSource();

            // ネットワーク所属はマスタの接続paramが答える。blockTypeの列挙は新種別を取りこぼす
            // The master's connection params answer which networks a block joins; enumerating blockTypes drops new kinds
            var param = block.BlockMasterElement.BlockParam;
            IsTrackingGear = param is IGearConnectors;
            CleanRoomTarget = ToCleanRoomTarget(param);
            if (param is IElectricWireConnectParam) _electricPoller.Start(block.BlockInstanceId);
            if (IsTrackingGear) _gearPoller.Start(block.BlockInstanceId);
            if (block.BlockMasterElement.BlockType == "FilterSplitter") FetchFilterSplitter(block.BlockPosInfo.OriginalPos, _cts.Token).Forget();

            // 室内機械と清浄機は、開いている間ずっと部屋の状態を追う
            // Interior machines and purifiers follow the room state while open
            if (CleanRoomTarget != CleanRoomDetailTarget.None) _cleanRoomPoller.Start(block.BlockInstanceId);
        }

        public void Clear()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _electricPoller.Stop();
            _gearPoller.Stop();
            _cleanRoomPoller.Stop();
            FilterSplitter = null;
            IsTrackingGear = false;
            CleanRoomTarget = CleanRoomDetailTarget.None;
            _currentPos = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        // action handler が SetMode/SetFilterItem 応答のスナップショットを反映する書き込み口
        // Write access for action handlers to apply SetMode/SetFilterItem response snapshots
        public void ApplyFilterSplitterSnapshot(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            FilterSplitter = response;
            _onUpdated.OnNext(Unit.Default);
        }

        // 部屋欄を出すブロックの判定はここだけ。部屋に属しうる室内機械と清浄機のほかは部屋欄を持たない
        // The only place deciding which blocks show the room section; only interior machines and purifiers have one
        private static CleanRoomDetailTarget ToCleanRoomTarget(IBlockParam param)
        {
            return param switch
            {
                CleanRoomMachineBlockParam => CleanRoomDetailTarget.Machine,
                CleanRoomAirFilterBlockParam => CleanRoomDetailTarget.AirFilter,
                _ => CleanRoomDetailTarget.None,
            };
        }

        private async UniTaskVoid FetchFilterSplitter(Vector3Int pos, CancellationToken ct)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(pos);
            var response = await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, ct);
            if (ct.IsCancellationRequested) return;
            // 取得失敗（未配置/別ブロック）は空スナップショットで上書きせず無視する（uGUI と同じガード）
            // Ignore fetch failures instead of overwriting with an empty snapshot (same guard as uGUI)
            if (response == null || !response.Success) return;
            ApplyFilterSplitterSnapshot(response);
        }
    }
}
