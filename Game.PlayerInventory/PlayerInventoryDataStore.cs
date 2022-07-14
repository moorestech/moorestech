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
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;
        private readonly CraftingEvent _craftingEvent;
        
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            ICraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService, IGrabInventoryUpdateEvent grabInventoryUpdateEvent, ICraftingEvent craftingEvent)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent) mainInventoryUpdateEvent;
            _craftInventoryUpdateEvent = (CraftInventoryUpdateEvent) craftInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent) grabInventoryUpdateEvent;
            _craftingEvent = (CraftingEvent)craftingEvent;
            
            _itemStackFactory = itemStackFactory;
            _isCreatableJudgementService = isCreatableJudgementService;
        }

        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, _itemStackFactory);
                var craft = new CraftingOpenableInventoryData(playerId, _craftInventoryUpdateEvent, _itemStackFactory,_isCreatableJudgementService,main,grab,_craftingEvent);

                _playerInventoryData.Add(playerId, new PlayerInventoryData(main,craft,grab));
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
                var (mainItems, craftItems,grabItem) = saveInventory.GetPlayerInventoryData(_itemStackFactory);

                //アイテムを復元
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _itemStackFactory,mainItems);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, _itemStackFactory,grabItem);
                var craftingInventory = new CraftingOpenableInventoryData(playerId, _craftInventoryUpdateEvent, _itemStackFactory,_isCreatableJudgementService,craftItems,main,grab);

                var playerInventory = new PlayerInventoryData(main, craftingInventory,grab);
                
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