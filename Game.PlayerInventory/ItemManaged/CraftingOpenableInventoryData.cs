using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class CraftingOpenableInventoryData : ICraftingOpenableInventory
    {
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;
        private readonly int _playerId;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        private readonly GrabInventoryData _grabInventoryData;
        private readonly MainOpenableInventoryData _mainOpenableInventoryData;
        
        private readonly CraftInventoryUpdateEvent _craftInventoryUpdateEvent;
        private readonly CraftingEvent _craftingEvent;

        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService, MainOpenableInventoryData mainOpenableInventoryData, GrabInventoryData grabInventoryData, ICraftingEvent craftingEvent)
        {
            _playerId = playerId;
            
            _craftInventoryUpdateEvent = craftInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;
            _isCreatableJudgementService = isCreatableJudgementService;
            _mainOpenableInventoryData = mainOpenableInventoryData;
            _grabInventoryData = grabInventoryData;
            _craftingEvent = craftingEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, 
                itemStackFactory, PlayerInventoryConst.CraftingSlotSize);
        }
        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent, ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService,List<IItemStack> itemStacks, MainOpenableInventoryData mainOpenableInventoryData, GrabInventoryData grabInventoryData, ICraftingEvent craftingEvent) : 
            this(playerId, craftInventoryUpdateEvent, itemStackFactory,isCreatableJudgementService, mainOpenableInventoryData, grabInventoryData, craftingEvent)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                _openableInventoryService.SetItemWithoutEvent(i,itemStacks[i]);
            }
        }

        #region CraftLogic
        
        public void NormalCraft()
        {
            //クラフトが可能なアイテムの配置かチェック
            //クラフト結果のアイテムを持ちスロットに追加可能か判定
            if (!IsCreatable()) return;
            
            //クラフト結果のアイテムを取得しておく
            var result = _isCreatableJudgementService.GetResult(CraftingItems);
            if (!_grabInventoryData.GetItem(0).IsAllowedToAdd(result)) return;

            //クラフトしたアイテムを消費する
            ConsumptionCraftItem(1, CraftingItems);
            
            //元のクラフト結果のアイテムを足したアイテムを持ちインベントリに追加
            var outputSlotItem = _grabInventoryData.GetItem(0);
            var addedOutputSlot = outputSlotItem.AddItem(result).ProcessResultItemStack;
            _grabInventoryData.SetItem(0, addedOutputSlot);
        }

        public void AllCraft()
        {
            var craftNum = _isCreatableJudgementService.CalcAllCraftItemNum(CraftingItems,_mainOpenableInventoryData.Items);
            var result = _isCreatableJudgementService.GetResult(CraftingItems);
            for (int i = 0; i < craftNum; i++)
            {
                _mainOpenableInventoryData.InsertItem(result);
            }
            ConsumptionCraftItem(craftNum, CraftingItems);
        }

        public void OneStackCraft()
        {
            var craftNum = _isCreatableJudgementService.CalcOneStackCraftItemNum(CraftingItems,_mainOpenableInventoryData.Items);
            var result = _isCreatableJudgementService.GetResult(CraftingItems);
            for (int i = 0; i < craftNum; i++)
            {
                _mainOpenableInventoryData.InsertItem(result);
            }
            ConsumptionCraftItem(craftNum, CraftingItems);
        }


        private void ConsumptionCraftItem(int itemCount,IReadOnlyList<IItemStack> craftingItems)
        {
            for (int i = 0; i < itemCount; i++)
            {
                var craftConfig = _isCreatableJudgementService.GetCraftingConfigData(craftingItems);
                for (int j = 0; j < PlayerInventoryConst.CraftingSlotSize; j++)
                {
                    //クラフトしたアイテムを消費する
                    var subItem = _openableInventoryService.Inventory[j].SubItem(craftConfig.Items[j].Count);
                    //インベントリにセット
                    _openableInventoryService.SetItem(j, subItem);
                }
            }
        }

        #endregion
        
        
        

        public IItemStack GetCreatableItem() { return　IsCreatable() ? _isCreatableJudgementService.GetResult(CraftingItems) : _itemStackFactory.CreatEmpty(); }
        public bool IsCreatable() { return _isCreatableJudgementService.IsCreatable(CraftingItems); }

        private IReadOnlyList<IItemStack> CraftingItems => _openableInventoryService.Inventory;

        

        #region delgate to PlayerInventoryItemDataStoreService
        public ReadOnlyCollection<IItemStack> Items => _openableInventoryService.Items;
        public IItemStack GetItem(int slot) { return _openableInventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _openableInventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _openableInventoryService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _openableInventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _openableInventoryService.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _openableInventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _openableInventoryService.InsertItem(itemId, count); }
        public int GetSlotSize() { return _openableInventoryService.GetSlotSize(); }
        
        
        private void InvokeEvent(int slot, IItemStack itemStack) { _craftInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,itemStack)); }

        #endregion
    }
}