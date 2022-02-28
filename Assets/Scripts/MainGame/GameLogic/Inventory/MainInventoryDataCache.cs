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
        private readonly BlockInventoryItemView _blockInventoryItemView;
        private readonly HotBarItemView _hotBarItemView;
        
        private List<ItemStack> _items = new(new ItemStack[PlayerInventoryConstant.MainInventorySize]);
        
        public MainInventoryDataCache(
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,MainInventoryItemView mainInventoryItemView,BlockInventoryItemView blockInventoryItemView,
            HotBarItemView hotBarItemView)
        {
            mainInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
            _mainInventoryItemView = mainInventoryItemView;
            _blockInventoryItemView = blockInventoryItemView;
            _hotBarItemView = hotBarItemView;
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
                
                //viewのUIにインベントリが更新されたことを通知する処理をキューに入れる
                MainThreadExecutionQueue.Instance.Insert(() =>
                {
                    _mainInventoryItemView.OnInventoryUpdate(slot, id, count);
                    _hotBarItemView.OnInventoryUpdate(slot, id, count);
                    _blockInventoryItemView.MainInventoryUpdate(slot,id,count);
                });
            }
        }

        public void UpdateSlotInventory(MainInventorySlotUpdateProperties properties)
        {
            var slot = properties.SlotId;
            _items[slot] = properties.ItemStack;
            
            
            var id = _items[slot].ID;
            var count = _items[slot].Count;
            
            //イベントの発火
            MainThreadExecutionQueue.Instance.Insert(() =>
            {
                _mainInventoryItemView.OnInventoryUpdate(slot, id, count);
                _hotBarItemView.OnInventoryUpdate(slot, id, count);
                _blockInventoryItemView.MainInventoryUpdate(slot,id,count);
            });
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}