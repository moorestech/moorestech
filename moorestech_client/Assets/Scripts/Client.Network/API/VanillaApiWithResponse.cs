using System;
using System.Collections.Generic;
using System.Threading;
using Client.Network.Settings;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Train.RailPositions;
using Game.PlayerRiding.Interface;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly IItemStackFactory _itemStackFactory;
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;
        // 同一アセンブリの送信APIへ接続情報を共有する
        // Share connection dependencies with request APIs in this assembly
        internal readonly PacketExchangeManager PacketExchange;
        internal readonly PlayerConnectionSetting ConnectionSetting;

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _itemStackFactory = ServerContext.ItemStackFactory;
            _itemStackLevelUnlocker = ServerContext.GetService<IItemStackLevelUnlocker>();
            PacketExchange = packetExchangeManager;
            ConnectionSetting = playerConnectionSetting;
        }

        public async UniTask<InitialHandshakeResponse> InitialHandShake(int playerId, CancellationToken ct)
        {
            // 最初のハンドシェイクを行う
            // Perform the initial handshake
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}");
            var initialHandShake = await PacketExchange.GetPacketResponse<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(request, ct);

            // ハンドシェイクに同梱されたスタックレベルを先に適用（インベントリ等のItemStack生成前に上限を正すため）
            // Apply stack levels bundled in the handshake first so ItemStacks built from later responses use correct limits
            foreach (var itemStackLevel in initialHandShake.ItemStackLevels)
            {
                _itemStackLevelUnlocker.UnlockStackLevel(itemStackLevel.ItemGuid, itemStackLevel.Level);
            }

            //必要なデータを取得する
            // Fetch all required resources
            var responses = await UniTask.WhenAll(
                this.GetMapObjectInfo(ct),
                this.GetWorldData(ct),
                GetPlayerInventory(playerId, ct),
                this.GetChallengeResponse(ct),
                this.GetUnlockState(ct),
                this.GetPlayedSkitIds(ct),
                this.GetResearchNodeStates(ct),
                GetMapData(ct));

            return new InitialHandshakeResponse(initialHandShake, responses);
        }

        public async UniTask<PlayerInventoryResponse> GetMyPlayerInventory(CancellationToken ct)
        {
            return await GetPlayerInventory(ConnectionSetting.PlayerId, ct);
        }

        public async UniTask<PlayerInventoryResponse> GetPlayerInventory(int playerId, CancellationToken ct)
        {
            var request = new PlayerInventoryResponseProtocol.RequestPlayerInventoryProtocolMessagePack(playerId);

            var response = await PacketExchange.GetPacketResponse<PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack>(request, ct);

            // メイン・Grabだけでなく装備と選択インデックスも落とさず変換する
            // Converts equipment and the selected index as well, not just main and grab
            return new PlayerInventoryResponse(response);
        }

        public async UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await PacketExchange.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Result);
        }

        // 時刻表置換と自動運転切替を単一の送信口で扱う
        // Send timetable replacement and auto-run changes through one entry point
        public async UniTask<TrainScheduleEditProtocol.TrainScheduleEditResponse> SendTrainScheduleEdit(
            TrainScheduleEditProtocol.TrainScheduleEditRequest request, CancellationToken ct)
        {
            return await PacketExchange.GetPacketResponse<TrainScheduleEditProtocol.TrainScheduleEditResponse>(request, ct);
        }

        // 改名結果を待ち、表示更新はブロック状態の通知に委ねる
        // Await the rename result; block state notifications update the displayed name
        public async UniTask<SetTrainStationNameProtocol.SetTrainStationNameResponse> SetTrainStationName(
            Vector3Int position, string stationName, CancellationToken ct)
        {
            var request = new SetTrainStationNameProtocol.SetTrainStationNameRequest(position, stationName);
            return await PacketExchange.GetPacketResponse<SetTrainStationNameProtocol.SetTrainStationNameResponse>(request, ct);
        }

        public async UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)
        {
            // 列車設置のレスポンスを取得する
            // Get response for train placement
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new PlaceTrainCarOnRailProtocol.PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarGuid, ConnectionSetting.PlayerId);
            return await PacketExchange.GetPacketResponse<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack>(request, ct);
        }

        // 乗車/降車をサーバーに要求し、結果を受け取る（仕様書セクション5.1）。
        // Requests ride/dismount from the server and returns the result.
        public async UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
        {
            var request = new RideActionProtocol.RequestRideActionMessagePack(ConnectionSetting.PlayerId, action, target);
            return await PacketExchange.GetPacketResponse<RideActionProtocol.ResponseRideActionMessagePack>(request, ct);
        }

        // マップレイアウト（spawn/mapObjects/mapVeins）をハンドシェイク時に取得する
        // Fetch the map layout (spawn/mapObjects/mapVeins) during the handshake
        public async UniTask<GetMapDataProtocol.ResponseMapDataMessagePack> GetMapData(CancellationToken ct)
        {
            var request = GetMapDataProtocol.RequestMapDataMessagePack.CreateLayoutRequest();
            return await PacketExchange.GetPacketResponse<GetMapDataProtocol.ResponseMapDataMessagePack>(request, ct);
        }

        private List<IItemStack> CreateStacks(ItemMessagePack[] items)
        {
            // メッセージパックからアイテムスタックを生成
            // Create item stacks from message pack items
            var count = items?.Length ?? 0;
            var stacks = new List<IItemStack>(count);
            if (items == null) return stacks;
            foreach (var item in items)
            {
                stacks.Add(_itemStackFactory.Create(item.Id, item.Count));
            }
            return stacks;
        }
    }
}
