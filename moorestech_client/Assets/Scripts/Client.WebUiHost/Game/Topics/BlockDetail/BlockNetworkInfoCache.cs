using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 開いているブロックのネット集約情報をキャッシュ
    /// - electric/gear: 1秒ポーリング
    /// - filter: 開時1回取得
    /// Caches network aggregates for the open block
    /// - electric/gear: 1s polling
    /// - filter: fetched once on open
    /// </summary>
    public class BlockNetworkInfoCache
    {
        public GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot Electric { get; private set; }
        public GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot GearNetwork { get; private set; }
        public FilterSplitterStateProtocol.FilterSplitterStateResponse FilterSplitter { get; private set; }
        public event Action OnUpdated;

        private CancellationTokenSource _cts;
        private Vector3Int _currentPos = new(int.MinValue, int.MinValue, int.MinValue);

        // 開いたブロックに合わせて取得を開始する。ブロックが変わったら前の取得を打ち切る
        // Start fetching for the opened block; cancel previous fetches when the block changes
        public void Track(BlockGameObject block, bool electric, bool gear, bool filterSplitter)
        {
            if (block != null && block.BlockPosInfo.OriginalPos == _currentPos && _cts != null) return;
            Clear();
            if (block == null) return;
            _currentPos = block.BlockPosInfo.OriginalPos;
            _cts = new CancellationTokenSource();
            if (electric) PollElectric(block, _cts.Token).Forget();
            if (gear) PollGear(block, _cts.Token).Forget();
            if (filterSplitter) FetchFilterSplitter(block.BlockPosInfo.OriginalPos, _cts.Token).Forget();
        }

        public void Clear()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            Electric = null;
            GearNetwork = null;
            FilterSplitter = null;
            _currentPos = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        // action handler が SetMode/SetFilterItem 応答のスナップショットを反映する書き込み口
        // Write access for action handlers to apply SetMode/SetFilterItem response snapshots
        public void ApplyFilterSplitterSnapshot(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            FilterSplitter = response;
            OnUpdated?.Invoke();
        }

        private async UniTaskVoid PollElectric(BlockGameObject block, CancellationToken ct)
        {
            // uGUI ElectricNetworkInfoView と同じ1秒間隔ポーリング
            // Same 1-second polling as uGUI ElectricNetworkInfoView
            while (!ct.IsCancellationRequested)
            {
                var response = await ClientContext.VanillaApi.Response.GetElectricNetworkInfo(block.BlockInstanceId, ct);
                if (ct.IsCancellationRequested) return;
                Electric = response?.Info;
                OnUpdated?.Invoke();
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
            }
        }

        private async UniTaskVoid PollGear(BlockGameObject block, CancellationToken ct)
        {
            // PollElectric と同じ1秒間隔ポーリング（開いている間ずっと停止理由行を追随させる）
            // Same 1-second polling as PollElectric, so the stop-reason row stays live while open
            while (!ct.IsCancellationRequested)
            {
                var response = await ClientContext.VanillaApi.Response.GetGearNetworkInfo(block.BlockInstanceId, ct);
                if (ct.IsCancellationRequested) return;
                GearNetwork = response?.Info;
                OnUpdated?.Invoke();
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
            }
        }

        private async UniTaskVoid FetchFilterSplitter(Vector3Int pos, CancellationToken ct)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(pos);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, ct);
            if (ct.IsCancellationRequested) return;
            // 取得失敗（未配置/別ブロック）は空スナップショットで上書きせず無視する（uGUI と同じガード）
            // Ignore fetch failures instead of overwriting with an empty snapshot (same guard as uGUI)
            if (response == null || !response.Success) return;
            ApplyFilterSplitterSnapshot(response);
        }
    }
}
