using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public class MainInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.Main;

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MainInventoryIdentifierResolver(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier, int requesterPlayerId)
        {
            // 接続に紐づくPlayerIdからメインインベントリを取得する
            // Get the main inventory from the player id bound to the connection.
            return _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
        }
    }
}
