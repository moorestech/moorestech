using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.GameLogic.Inventory
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class MainInventoryDataCache : IInitializable
    {
        private readonly MainInventoryItemView _mainInventoryItemView;
        private readonly HotBarItemView _hotBarItemView;
        
        private List<ItemStack> _items = new(new ItemStack[PlayerInventoryConstant.MainInventorySize]);
        
        public MainInventoryDataCache(
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,MainInventoryItemView mainInventoryItemView,
            HotBarItemView hotBarItemView)
        {
            mainInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
            _mainInventoryItemView = mainInventoryItemView;
            _hotBarItemView = hotBarItemView;
        }

        public void UpdateInventory(MainInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
                var slot = i;
                MainThreadExecutionQueue.Instance.Insert(() => _mainInventoryItemView.OnInventoryUpdate(slot,_items[slot].ID,_items[slot].Count));
                MainThreadExecutionQueue.Instance.Insert(() => _hotBarItemView.OnInventoryUpdate(slot,_items[slot].ID,_items[slot].Count));
            }
        }

        public void UpdateSlotInventory(MainInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            //イベントの発火
            
            //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
            MainThreadExecutionQueue.Instance.Insert(() => _mainInventoryItemView.OnInventoryUpdate(s,_items[s].ID,_items[s].Count));
            MainThreadExecutionQueue.Instance.Insert(() => _hotBarItemView.OnInventoryUpdate(s,_items[s].ID,_items[s].Count));
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}