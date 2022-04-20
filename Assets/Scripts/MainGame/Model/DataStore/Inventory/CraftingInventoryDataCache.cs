using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;

namespace MainGame.GameLogic.Inventory
{
    public class CraftingInventoryDataCache
    {
        
        private List<ItemStack> _items = new(new ItemStack[PlayerInventoryConstant.CraftingInventorySize]);
        
        public CraftingInventoryDataCache(
            ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            craftingInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(CraftingInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
                var slot = i;
                //todo to event MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.OnInventoryUpdate(slot,_items[slot]));
            }
            //結果のアイテムの設定
            //todo to event MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.SetResultItem(properties.ResultItemStack,properties.CanCraft));
        }

        public void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            
            //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
            //todo event MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.OnInventoryUpdate(s,_items[s]));
            //結果のアイテムの設定
            //todo event MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.SetResultItem(properties.ResultItemStack,properties.CanCraft));
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}