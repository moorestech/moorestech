using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory
{
    /// <summary>
    /// プレイヤーインベントリのデータを扱います。
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryDataStore(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            ItemStackFactory itemStackFactory)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _playerInventoryUpdateEvent = (PlayerInventoryUpdateEvent) playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;
        }

        public IInventory GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                _playerInventoryData.Add(playerId,
                    new PlayerInventoryData(playerId, _playerInventoryUpdateEvent, _itemStackFactory));
            }

            return _playerInventoryData[playerId];
        }

        public List<SaveInventoryData> GetSaveInventoryDataList()
        {
            var savePlayerInventoryList =  new List<SaveInventoryData>();
            //セーブデータに必要なデータをまとめる
            foreach (var inventory in _playerInventoryData)
            {
                var itemIds = new List<int>();
                var itemCounts = new List<int>();
                for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                {
                    var item = inventory.Value.GetItem(i);
                    itemIds.Add(item.Id);
                    itemCounts.Add(item.Count);
                }
                var saveInventoryData = new SaveInventoryData(inventory.Key, itemIds, itemCounts);
                savePlayerInventoryList.Add(saveInventoryData);
            }
            
            return savePlayerInventoryList;
        }

        /// <summary>
        /// プレイヤーのデータを置き換える
        /// </summary>
        public void LoadPlayerInventory(List<SaveInventoryData> saveInventoryDataList)
        {
            foreach (var saveInventory in saveInventoryDataList)
            {
                var playerId = saveInventory.PlayerId;

                var inventory = new PlayerInventoryData(playerId, _playerInventoryUpdateEvent, _itemStackFactory);
                //インベントリの追加を行う　既にあるなら置き換える
                if (_playerInventoryData.ContainsKey(playerId))
                {
                    _playerInventoryData[playerId] = inventory;
                }else
                {
                    _playerInventoryData.Add(playerId,inventory);
                }
                
                //インベントリにアイテムを追加する
                for (int i = 0; i < saveInventory.ItemCount.Count; i++)
                {
                    inventory.SetItem(i,_itemStackFactory.Create(
                       saveInventory.ItemId[i],
                       saveInventory.ItemCount[i]));
                }
            }
        }
    }
}