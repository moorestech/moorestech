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
        private readonly CraftInventoryUpdateEvent _craftInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;
        private readonly IItemCraftingService _itemCraftingService;

        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService, IItemCraftingService itemCraftingService)
        {
            _playerId = playerId;
            
            _craftInventoryUpdateEvent = craftInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;
            _isCreatableJudgementService = isCreatableJudgementService;
            _itemCraftingService = itemCraftingService;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, 
                itemStackFactory, PlayerInventoryConst.CraftingSlotSize);
        }
        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent, ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService,List<IItemStack> itemStacks, IItemCraftingService itemCraftingService) : 
            this(playerId, craftInventoryUpdateEvent, itemStackFactory,isCreatableJudgementService, itemCraftingService)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                _openableInventoryService.SetItemWithoutEvent(i,itemStacks[i]);
            }
        }

        #region CraftLogic
        
        public void NormalCraft() { _itemCraftingService.NormalCraft(); }

        public void AllCraft() { _itemCraftingService.AllCraft(); }

        public void OneStackCraft() { _itemCraftingService.OneStackCraft(); }

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