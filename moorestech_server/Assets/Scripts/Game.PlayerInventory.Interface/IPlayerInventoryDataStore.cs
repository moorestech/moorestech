using System.Collections.Generic;

namespace Game.PlayerInventory.Interface
{
    public interface IPlayerInventoryDataStore
    {
        public PlayerInventoryData GetInventoryData(int playerId);
        public List<PlayerInventoryJsonObject> GetSaveInventoryDataList();
        public void LoadPlayerInventory(List<PlayerInventoryJsonObject> saveInventoryDataList);
    }
}