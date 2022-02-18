using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.Craft.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;
using PlayerInventory.ItemManaged;

namespace PlayerInventory
{
    /// <summary>
    /// プレイヤーインベントリのデータを扱います。
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();
        private readonly PlayerMainInventoryUpdateEvent _playerMainInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        public PlayerInventoryDataStore(IPlayerMainInventoryUpdateEvent playerMainInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _playerMainInventoryUpdateEvent = (PlayerMainInventoryUpdateEvent) playerMainInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;
            _isCreatableJudgementService = isCreatableJudgementService;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainInventoryData(playerId, _playerMainInventoryUpdateEvent, _itemStackFactory);
                var craft = new CraftInventoryData(playerId, _playerMainInventoryUpdateEvent, _itemStackFactory,_isCreatableJudgementService);
                
                _playerInventoryData.Add(playerId, new PlayerInventoryData(main,craft));
            }

            return _playerInventoryData[playerId];
        }

        //TODO クラフトインベントリのデータも含める
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
                    var item = inventory.Value.MainInventory.GetItem(i);
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

                var inventory = new MainInventoryData(playerId, _playerMainInventoryUpdateEvent, _itemStackFactory);
                //インベントリの追加を行う　既にあるなら置き換える
                if (_playerInventoryData.ContainsKey(playerId))
                {
                    _playerInventoryData[playerId] = new PlayerInventoryData(inventory,null);
                }else
                {
                    _playerInventoryData.Add(playerId,new PlayerInventoryData(inventory,null));
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