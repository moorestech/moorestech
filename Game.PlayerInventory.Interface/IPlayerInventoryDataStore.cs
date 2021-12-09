using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public interface IPlayerInventoryDataStore
    {
        public IInventory GetInventoryData(int playerId);
    }
}