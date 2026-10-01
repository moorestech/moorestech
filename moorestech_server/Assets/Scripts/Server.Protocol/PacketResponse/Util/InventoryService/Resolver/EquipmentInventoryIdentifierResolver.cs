using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public class EquipmentInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.Equipment;

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public EquipmentInventoryIdentifierResolver(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier, int requesterPlayerId)
        {
            // 接続に紐づくPlayerIdから装備インベントリを取得する
            // Get the equipment inventory from the player id bound to the connection.
            return _playerInventoryDataStore.GetInventoryData(requesterPlayerId).EquipmentInventory;
        }
    }
}
