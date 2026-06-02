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

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier)
        {
            // 識別子内のPlayerIdからメインインベントリを取得する
            // Get the main inventory from the player id in the identifier.
            return _playerInventoryDataStore.GetInventoryData(identifier.PlayerId).MainOpenableInventory;
        }
    }
}
