using System.Collections.Generic;
using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public interface IPlayerInventoryDataStore
    {
        public IInventory GetMainInventoryData(int playerId);
        public List<SaveInventoryData> GetSaveInventoryDataList();
        public void LoadPlayerInventory(List<SaveInventoryData> saveInventoryDataList);
    }
}