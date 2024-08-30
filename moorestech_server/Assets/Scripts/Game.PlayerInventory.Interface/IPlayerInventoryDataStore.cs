using System.Collections.Generic;

namespace Game.PlayerInventory.Interface
{
    public interface IPlayerInventoryDataStore
    {
        public PlayerInventoryData GetInventoryData(int playerId);
        public List<PlayerInventorySaveJsonObject> GetSaveJsonObject();
        public void LoadPlayerInventory(List<PlayerInventorySaveJsonObject> saveInventoryDataList);
    }
}