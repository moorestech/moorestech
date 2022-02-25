using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.View;

namespace MainGame.GameLogic.Inventory
{
    public class CraftingInventoryDataCache
    {
        private readonly CraftingInventoryItemView _craftingInventoryItemView;
        
        private List<ItemStack> _items = new(new ItemStack[PlayerInventoryConstant.MainInventorySize]);
        
        public CraftingInventoryDataCache(
            ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent,CraftingInventoryItemView craftingInventoryItemView)
        {
            _craftingInventoryItemView = craftingInventoryItemView;
            craftingInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(CraftingInventoryUpdateProperties properties)
        {
            //TODO 結果アイテムの表示やクラフト可否の表示を更新する
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
                var slot = i;
                MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.OnInventoryUpdate(slot,_items[slot].ID,_items[slot].Count));
            }
        }

        public void UpdateSlotInventory(CraftingInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            //イベントの発火
            
            //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
            MainThreadExecutionQueue.Instance.Insert(() => _craftingInventoryItemView.OnInventoryUpdate(s,_items[s].ID,_items[s].Count));
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}