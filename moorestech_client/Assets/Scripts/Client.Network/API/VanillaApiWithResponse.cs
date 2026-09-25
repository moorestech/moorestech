using System.Collections.Generic;
using System.Threading;
using Client.Network.Settings;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerRiding.Interface;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Network.API
{
    public class VanillaApiWithResponse
    {
        private readonly IItemStackFactory _itemStackFactory;
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;
        private readonly PacketExchangeManager _packetExchange;
        private readonly PlayerConnectionSetting _connectionSetting;

        // 領域ごとのレスポンスAPIを合成で保持する
        // Hold the per-domain response APIs by composition
        public readonly BlockResponseApi Block;
        public readonly ConnectionResponseApi Connection;
        public readonly ProgressResponseApi Progress;
        public readonly TrainResponseApi Train;
        public readonly WorldResponseApi World;

        public VanillaApiWithResponse(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _itemStackFactory = ServerContext.ItemStackFactory;
            _itemStackLevelUnlocker = ServerContext.GetService<IItemStackLevelUnlocker>();
            _packetExchange = packetExchangeManager;
            _connectionSetting = playerConnectionSetting;

            Block = new BlockResponseApi(packetExchangeManager, playerConnectionSetting);
            Connection = new ConnectionResponseApi(packetExchangeManager, playerConnectionSetting);
            Progress = new ProgressResponseApi(packetExchangeManager, playerConnectionSetting);
            Train = new TrainResponseApi(packetExchangeManager, playerConnectionSetting);
            World = new WorldResponseApi(packetExchangeManager, playerConnectionSetting);
        }

        public async UniTask<InitialHandshakeResponse> InitialHandShake(int playerId, CancellationToken ct)
        {
            // 最初のハンドシェイクを行う
            // Perform the initial handshake
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}");
            var initialHandShake = await _packetExchange.GetPacketResponse<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(request, ct);

            // ハンドシェイクに同梱されたスタックレベルを先に適用（インベントリ等のItemStack生成前に上限を正すため）
            // Apply stack levels bundled in the handshake first so ItemStacks built from later responses use correct limits
            foreach (var itemStackLevel in initialHandShake.ItemStackLevels)
            {
                _itemStackLevelUnlocker.UnlockStackLevel(itemStackLevel.ItemGuid, itemStackLevel.Level);
            }

            //必要なデータを取得する
            // Fetch all required resources
            var responses = await UniTask.WhenAll(
                World.GetMapObjectInfo(ct),
                World.GetWorldData(ct),
                GetPlayerInventory(playerId, ct),
                Progress.GetChallengeResponse(ct),
                Progress.GetUnlockState(ct),
                Progress.GetPlayedSkitIds(ct),
                Progress.GetResearchNodeStates(ct),
                World.GetMapData(ct));

            return new InitialHandshakeResponse(initialHandShake, responses);
        }

        public async UniTask<PlayerInventoryResponse> GetMyPlayerInventory(CancellationToken ct)
        {
            return await GetPlayerInventory(_connectionSetting.PlayerId, ct);
        }

        public async UniTask<PlayerInventoryResponse> GetPlayerInventory(int playerId, CancellationToken ct)
        {
            var request = new PlayerInventoryResponseProtocol.RequestPlayerInventoryProtocolMessagePack(playerId);

            var response = await _packetExchange.GetPacketResponse<PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack>(request, ct);

            // メイン・Grabだけでなく装備と選択インデックスも落とさず変換する
            // Converts equipment and the selected index as well, not just main and grab
            return new PlayerInventoryResponse(response);
        }

        public async UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await _packetExchange.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Result);
        }

        // 乗車/降車をサーバーに要求し、結果を受け取る（仕様書セクション5.1）。
        // Requests ride/dismount from the server and returns the result.
        public async UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
        {
            var request = new RideActionProtocol.RequestRideActionMessagePack(_connectionSetting.PlayerId, action, target);
            return await _packetExchange.GetPacketResponse<RideActionProtocol.ResponseRideActionMessagePack>(request, ct);
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
