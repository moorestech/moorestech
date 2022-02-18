using System.Collections.Generic;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using PlayerInventory.Event;

namespace PlayerInventory.ItemManaged
{
    public class CraftingInventoryData : ICraftingInventory
    {
        private readonly PlayerInventoryItemDataStoreService _inventoryService;
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        public CraftingInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService)
        {
            _isCreatableJudgementService = isCreatableJudgementService;
            _inventoryService = new PlayerInventoryItemDataStoreService(playerId, craftInventoryUpdateEvent, 
                itemStackFactory, PlayerInventoryConst.CraftingInventorySize);
        }
        public CraftingInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService
            ,List<IItemStack> itemStacks) : 
            this(playerId, craftInventoryUpdateEvent, itemStackFactory,isCreatableJudgementService)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                _inventoryService.SetItemWithoutEvent(i,itemStacks[i]);
            }
        }

        public void Craft()
        {
            //クラフトが可能なアイテムの配置かチェック
            if (!_isCreatableJudgementService.IsCreatable(CraftingItems)) return;
            
            //クラフト結果のアイテムを出力スロットに追加可能か判定
            var result = _isCreatableJudgementService.GetResult(CraftingItems);
            var outputItem = _inventoryService.GetItem(PlayerInventoryConst.CraftingInventorySize - 1);

            //クラフトしたアイテムの出力スロットに空きがある
            if (!outputItem.IsAllowedToAdd(result)) return;
            //クラフトしたアイテムを消費する
            var craftConfig = _isCreatableJudgementService.GetCraftingConfigData(CraftingItems);
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                //クラフトしたアイテムを消費する
                var subItem = _inventoryService.Inventory[i].SubItem(craftConfig.Items[i].Count);
                //インベントリにセット
                _inventoryService.SetItem(i, subItem);
            }
            
            
            //元のクラフト結果のアイテムを足したアイテムを出力スロットに追加
            var addedOutputSlot = outputItem.AddItem(result).ProcessResultItemStack;
            _inventoryService.SetItem(PlayerInventoryConst.CraftingInventorySize - 1, addedOutputSlot);
        }

        public IItemStack GetCreatableItem() { return _isCreatableJudgementService.GetResult(CraftingItems); }
        public bool IsCreatable() { return _isCreatableJudgementService.IsCreatable(CraftingItems); }
        private List<IItemStack> CraftingItems
        {
            get
            {
                var items = new List<IItemStack>();
                for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                {
                    items.Add(_inventoryService.GetItem(i));
                }
                return items;
            }
        }


        #region delgate to PlayerInventoryItemDataStoreService
        public IItemStack GetItem(int slot) { return _inventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _inventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _inventoryService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _inventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _inventoryService.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _inventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _inventoryService.InsertItem(itemId, count); }
        public int GetSlotSize() { return _inventoryService.GetSlotSize(); }

        #endregion
    }
}