using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;

namespace Game.PlayerInventory
{
    public class ChainInventoryService : IChainInventoryService
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public ChainInventoryService(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public bool TryConsumeChainItem(int playerId, ItemId chainItemId)
        {
            var inventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
            {
                var stack = inventory.GetItem(i);
                if (stack.Id != chainItemId || stack.Count <= 0) continue;
                var after = stack.SubItem(1);
                inventory.SetItem(i, after);
                return true;
            }

            return false;
        }
    }
}
