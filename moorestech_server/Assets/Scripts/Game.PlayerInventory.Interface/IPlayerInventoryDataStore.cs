using System.Collections.Generic;

namespace Game.PlayerInventory.Interface
{
    public interface IPlayerInventoryDataStore
    {
        public List<int> GetAllPlayerId();
        public PlayerInventoryData GetInventoryData(int playerId);

        // 接続確定時に呼ぶ。初期装備を受け取り済み・セーブ復元済みのプレイヤーには何もしない
        // Called when a connection is established; does nothing for a player already granted or restored from a save
        public void GrantInitialEquipmentIfNewPlayer(int playerId);
        
        public List<PlayerInventorySaveJsonObject> GetSaveJsonObject();
        public void LoadPlayerInventory(List<PlayerInventorySaveJsonObject> saveInventoryDataList);
    }
}