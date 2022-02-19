using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.Crafting.Interface;
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
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly CraftInventoryUpdateEvent _craftInventoryUpdateEvent;
        
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            ICraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent) mainInventoryUpdateEvent;
            _craftInventoryUpdateEvent = (CraftInventoryUpdateEvent) craftInventoryUpdateEvent;
            
            _itemStackFactory = itemStackFactory;
            _isCreatableJudgementService = isCreatableJudgementService;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory);
                var craft = new CraftingInventoryData(playerId, _craftInventoryUpdateEvent, _itemStackFactory,_isCreatableJudgementService);
                
                _playerInventoryData.Add(playerId, new PlayerInventoryData(main,craft));
            }

            return _playerInventoryData[playerId];
        }

        public List<SaveInventoryData> GetSaveInventoryDataList()
        {
            var savePlayerInventoryList =  new List<SaveInventoryData>();
            //セーブデータに必要なデータをまとめる
            foreach (var inventory in _playerInventoryData)
            {
                var saveInventoryData = new SaveInventoryData(inventory.Key, inventory.Value);
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
                var (main, craft) = saveInventory.GetPlayerInventoryData(_itemStackFactory);

                //アイテムを復元
                var mainInventory = new MainInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory,main);
                var craftingInventory = new CraftingInventoryData(playerId, _craftInventoryUpdateEvent,
                    _itemStackFactory,_isCreatableJudgementService,craft);
                var playerInventory = new PlayerInventoryData(mainInventory, craftingInventory);
                
                //インベントリの追加を行う　既にあるなら置き換える
                if (_playerInventoryData.ContainsKey(playerId))
                {
                    _playerInventoryData[playerId] = playerInventory;
                }else
                {
                    _playerInventoryData.Add(playerId,playerInventory);
                }
            }
        }
    }
}