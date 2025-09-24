using System.Collections.Generic;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.ItemManaged;

namespace Game.PlayerInventory
{
    /// <summary>
    ///     プレイヤーインベントリのデータを扱います。
    ///     TODO プレイヤーのエンティティ内で管理すべきか検討中
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;
        
        
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();
        
        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent, IGrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent)mainInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent)grabInventoryUpdateEvent;
        }
        
        public List<int> GetAllPlayerId()
        {
            return new List<int>(_playerInventoryData.Keys);
        }
        
        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent);
                
                _playerInventoryData.Add(playerId, new PlayerInventoryData(main, grab));
            }
            
            return _playerInventoryData[playerId];
        }
        
        public List<PlayerInventorySaveJsonObject> GetSaveJsonObject()
        {
            var savePlayerInventoryList = new List<PlayerInventorySaveJsonObject>();
            //セーブデータに必要なデータをまとめる
            foreach (var inventory in _playerInventoryData)
            {
                var saveInventoryData = new PlayerInventorySaveJsonObject(inventory.Key, inventory.Value);
                savePlayerInventoryList.Add(saveInventoryData);
            }
            
            return savePlayerInventoryList;
        }
        
        /// <summary>
        ///     プレイヤーのデータを置き換える
        /// </summary>
        public void LoadPlayerInventory(List<PlayerInventorySaveJsonObject> saveInventoryDataList)
        {
            foreach (var saveInventory in saveInventoryDataList)
            {
                var playerId = saveInventory.PlayerId;
                (var mainItems, var grabItem) = saveInventory.GetPlayerInventoryData();
                
                //アイテムを復元
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, mainItems);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, grabItem);
                
                var playerInventory = new PlayerInventoryData(main, grab);
                
                //インベントリの追加を行う　既にあるなら置き換える
                _playerInventoryData[playerId] = playerInventory;
            }
        }
    }
}