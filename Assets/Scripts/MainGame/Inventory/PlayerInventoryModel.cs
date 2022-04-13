using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using MainGame.Basic;
using SinglePlay;
using UnityEngine;

namespace MainGame.Inventory
{
    public class PlayerInventoryModel
    {
        public IReadOnlyList<ItemStack> MainInventory => _mainInventory.Select(item => item.ToStructItemStack()).ToList();

        private readonly List<IItemStack> _mainInventory = new ();
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        private IItemStack _equippedItem;

        public bool IsEquipped => _isEquipped;

        private bool _isEquipped;


        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<int> OnDragSlot;
        public event Action<int> OnDragEndSlot;
        public event Action<ItemStack> OnEquippedItemUpdate;
        public event Action OnItemEquipped;
        public event Action OnItemUnequipped;
        


        public PlayerInventoryModel(ItemStackFactory itemStackFactory, IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _itemStackFactory = itemStackFactory;
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                _mainInventory.Add(itemStackFactory.CreatEmpty());
            }
        }



        public void EquippedItem(int slot)
        {
            SetEquippedWithInvokeEvent(true,_mainInventory[slot]);
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.CreatEmpty());
        }
        public void EquippedHalfItem(int slot)
        {
            var equippedItemNum = _mainInventory[slot].Count/2;
            var slotItemNum = _mainInventory[slot].Count - equippedItemNum;
            var id = _mainInventory[slot].Id;
            
            SetEquippedWithInvokeEvent(true,_itemStackFactory.Create(id,equippedItemNum));
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.Create(id,slotItemNum));
        }

        public void PlaceItem(int slot)
        {
            var item = _mainInventory[slot];
            //アイテムを足しても余らない時はそのままおく
            if (item.IsAllowedToAdd(_equippedItem))
            {
                var result = item.AddItem(_equippedItem);
                SetInventoryWithInvokeEvent(slot,result.ProcessResultItemStack);
                SetEquippedWithInvokeEvent(false);
            }
            //あまりがでて、アイテム数が最大じゃない時は加算して、あまりをEquippedに入れる
            else if (item.IsAllowedToAddWithRemain(_equippedItem) && item.Count != _itemConfig.GetItemConfig(item.Id).MaxStack)
            {
                var result = item.AddItem(_equippedItem);
                
                SetInventoryWithInvokeEvent(slot,result.ProcessResultItemStack);
                SetEquippedWithInvokeEvent(true,result.RemainderItemStack);
            }
            //加算できない時か最大数がスロットにある時はアイテムを入れ替える
            else
            {
                var w = item;
                SetInventoryWithInvokeEvent(slot,_equippedItem);
                SetEquippedWithInvokeEvent(true,w);
            }
        }
        

        public void PlaceOneItem(int slot)
        {
            var addItem = _itemStackFactory.Create(_equippedItem.Id, 1);
            //アイテムを1個置ける時だけアイテムをおく
            if (_mainInventory[slot].IsAllowedToAdd(addItem))
            {
                //アイテムを加算する
                SetInventoryWithInvokeEvent(slot,_mainInventory[slot].AddItem(addItem).ProcessResultItemStack);
                
                //持っているアイテムを減らす
                var newEquippedItem = _equippedItem.SubItem(1);
                //持っているアイテムがなくなったら持ち状態を解除する
                if (newEquippedItem.Count == 0)
                {
                    SetEquippedWithInvokeEvent(false);
                }
                else
                {
                    SetEquippedWithInvokeEvent(true,newEquippedItem);
                }
            }
        }
        
        public void DragStartSlot(int slot)
        {
        }

        public void DragSlot(int slot)
        {
            
        }
        
        public void DragEndSlot(int slot)
        {
        }
        
        
        private void SetEquippedWithInvokeEvent(bool isEquipped,IItemStack itemStack = null)
        {
            _equippedItem = itemStack ?? _itemStackFactory.CreatEmpty();
            _isEquipped = isEquipped;
            
            if (isEquipped)
            {
                OnItemEquipped?.Invoke();
                OnEquippedItemUpdate?.Invoke(_equippedItem.ToStructItemStack());
            }
            else
            {
                OnItemUnequipped?.Invoke();
            }
        }
        private void SetInventoryWithInvokeEvent(int slot,IItemStack itemStack)
        {
            _mainInventory[slot] = itemStack;
            OnSlotUpdate?.Invoke(slot,itemStack.ToStructItemStack());
        }

    }
    
    public static class ItemStackExtend{
        public static ItemStack ToStructItemStack(this  IItemStack itemStack)
        {
            return new ItemStack(itemStack.Id,itemStack.Count);
        }
    }
}