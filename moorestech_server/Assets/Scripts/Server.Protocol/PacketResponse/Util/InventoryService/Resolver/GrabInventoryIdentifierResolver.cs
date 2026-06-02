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

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier)
        {
            // 識別子内のPlayerIdから手持ちインベントリを取得する
            // Get the grab inventory from the player id in the identifier.
            return _playerInventoryDataStore.GetInventoryData(identifier.PlayerId).GrabInventory;
        }
    }
}
