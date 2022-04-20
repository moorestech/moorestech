using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class MainInventoryDataCache : IInitializable
    {
        private List<ItemStack> _items = new(new ItemStack[PlayerInventoryConstant.MainInventorySize]);
        
        public MainInventoryDataCache(MainInventoryUpdateEvent mainInventoryUpdateEvent)
        {
            mainInventoryUpdateEvent.OnMainInventoryUpdateEvent +=UpdateInventory;
            mainInventoryUpdateEvent.OnMainInventorySlotUpdateEvent +=UpdateSlotInventory;
        }

        public void UpdateInventory(MainInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                var id = _items[i].ID;
                var count = _items[i].Count;
                var slot = i;
                
                
                //todo イベント _mainInventoryItemView.OnInventoryUpdate(slot, id, count);
                //_hotBarItemView.OnInventoryUpdate(slot, id, count);
                //_blockInventoryItemView.MainInventoryUpdate(slot,id,count);
            }
        }

        public void UpdateSlotInventory(MainInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            _items[slot] = properties.ItemStack;
            
            
            var id = _items[slot].ID;
            var count = _items[slot].Count;
            
            //イベントの発火
            
            //todo イベント _mainInventoryItemView.OnInventoryUpdate(slot, id, count);
            //_hotBarItemView.OnInventoryUpdate(slot, id, count);
            //_blockInventoryItemView.MainInventoryUpdate(slot,id,count);
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}