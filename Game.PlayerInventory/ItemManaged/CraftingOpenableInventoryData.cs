using System.Collections.Generic;
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
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;

        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService)
        {
            _playerId = playerId;
            
            _craftInventoryUpdateEvent = craftInventoryUpdateEvent;
            _isCreatableJudgementService = isCreatableJudgementService;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, 
                itemStackFactory, PlayerInventoryConst.CraftingInventorySize);
        }
        public CraftingOpenableInventoryData(int playerId, CraftInventoryUpdateEvent craftInventoryUpdateEvent, ItemStackFactory itemStackFactory,IIsCreatableJudgementService isCreatableJudgementService,List<IItemStack> itemStacks) : 
            this(playerId, craftInventoryUpdateEvent, itemStackFactory,isCreatableJudgementService)
        {
            for (int i = 0; i < itemStacks.Count; i++)
            {
                _openableInventoryService.SetItemWithoutEvent(i,itemStacks[i]);
            }
        }

        public void Craft()
        {
            //クラフトが可能なアイテムの配置かチェック
            if (!_isCreatableJudgementService.IsCreatable(CraftingItems)) return;
            
            //クラフト結果のアイテムを出力スロットに追加可能か判定
            var result = _isCreatableJudgementService.GetResult(CraftingItems);
            var outputItem = _openableInventoryService.GetItem(PlayerInventoryConst.CraftingInventorySize - 1);

            //クラフトしたアイテムの出力スロットに空きがある
            if (!outputItem.IsAllowedToAdd(result)) return;
            //クラフトしたアイテムを消費する
            var craftConfig = _isCreatableJudgementService.GetCraftingConfigData(CraftingItems);
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                //クラフトしたアイテムを消費する
                var subItem = _openableInventoryService.Inventory[i].SubItem(craftConfig.Items[i].Count);
                //インベントリにセット
                _openableInventoryService.SetItem(i, subItem);
            }
            
            
            //元のクラフト結果のアイテムを足したアイテムを出力スロットに追加
            var addedOutputSlot = outputItem.AddItem(result).ProcessResultItemStack;
            _openableInventoryService.SetItem(PlayerInventoryConst.CraftingInventorySize - 1, addedOutputSlot);
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
                    items.Add(_openableInventoryService.GetItem(i));
                }
                return items;
            }
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _craftInventoryUpdateEvent.OnInventoryUpdateInvoke(
                new PlayerInventoryUpdateEventProperties(_playerId,slot,itemStack));
        }


        #region delgate to PlayerInventoryItemDataStoreService
        public IItemStack GetItem(int slot) { return _openableInventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _openableInventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _openableInventoryService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _openableInventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _openableInventoryService.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(IItemStack itemStack) { return _openableInventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _openableInventoryService.InsertItem(itemId, count); }
        public int GetSlotSize() { return _openableInventoryService.GetSlotSize(); }

        #endregion
    }
}