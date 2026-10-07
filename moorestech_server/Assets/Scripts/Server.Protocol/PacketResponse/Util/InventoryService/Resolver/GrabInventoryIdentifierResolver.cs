using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public class GrabInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.Grab;

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public GrabInventoryIdentifierResolver(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier, int requesterPlayerId)
        {
            // 接続に紐づくPlayerIdから手持ちインベントリを取得する
            // Get the grab inventory from the player id bound to the connection.
            return _playerInventoryDataStore.GetInventoryData(requesterPlayerId).GrabInventory;
        }
    }
}
