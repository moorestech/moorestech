using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using MainGame.Basic;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class PlayerInventoryModelController
    {
        private readonly PlayerInventoryModel _playerInventoryModel;

        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        private IItemStack _equippedItem;

        public bool IsEquipped => _isEquipped;
        private bool _isEquipped;
        
        public bool IsItemSplitDragging => _isItemSplitDragging;
        public bool IsItemOneDragging => _isItemOneDragging;


        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<ItemStack> OnEquippedItemUpdate;
        public event Action OnItemEquipped;
        public event Action OnItemUnequipped;
        


        public PlayerInventoryModelController(ItemStackFactory itemStackFactory, IItemConfig itemConfig, PlayerInventoryModel playerInventoryModel)
        {
            _itemStackFactory = itemStackFactory;
            _itemConfig = itemConfig;
            _playerInventoryModel = playerInventoryModel;
        }

        #region EquippedPlaceItem

        public void EquippedItem(int slot)
        {
            SetEquippedWithInvokeEvent(true,_playerInventoryModel[slot]);
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.CreatEmpty());
        }
        public void EquippedHalfItem(int slot)
        {
            var equippedItemNum = _playerInventoryModel[slot].Count/2;
            var slotItemNum = _playerInventoryModel[slot].Count - equippedItemNum;
            var id = _playerInventoryModel[slot].Id;
            
            SetEquippedWithInvokeEvent(true,_itemStackFactory.Create(id,equippedItemNum));
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.Create(id,slotItemNum));
        }

        public void PlaceItem(int slot)
        {
            var item = _playerInventoryModel[slot];
            //アイテムを足しても余らない時はそのままおく
            if (item.IsAllowedToAdd(_equippedItem))
            {
                ItemSplitDragStart(slot,_equippedItem);
                var result = item.AddItem(_equippedItem);
                SetInventoryWithInvokeEvent(slot,result.ProcessResultItemStack);
                SetEquippedWithInvokeEvent(false);
            }
            //あまりがでて、アイテム数が最大じゃない時は加算して、あまりをEquippedに入れる
            else if (item.IsAllowedToAddWithRemain(_equippedItem) && item.Count != _itemConfig.GetItemConfig(item.Id).MaxStack)
            {
                ItemSplitDragStart(slot,_equippedItem);
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
            if (!_playerInventoryModel[slot].IsAllowedToAdd(addItem)) return;
            //アイテムを1個置ける時だけアイテムをおく
            
            
            //アイテムを加算する
            SetInventoryWithInvokeEvent(slot,_playerInventoryModel[slot].AddItem(addItem).ProcessResultItemStack);
                
            //持っているアイテムを減らす
            var newEquippedItem = _equippedItem.SubItem(1);
            if (newEquippedItem.Count == 0)
            {
                //持っているアイテムがなくなったら持ち状態を解除する
                SetEquippedWithInvokeEvent(false);
            }
            else
            {
                //なくなってない時は持っているアイテムを加算する
                ItemOneDragStart();
                SetEquippedWithInvokeEvent(true,newEquippedItem);
            }
        }

        #endregion

        #region SplitDrag

        private bool _isItemSplitDragging;
        private readonly List<ItemSplitDragSlot> _itemSplitDragSlots = new ();
        private IItemStack _dragStartEquippedItem;

        private void ItemSplitDragStart(int startSlot,IItemStack startEquippedItem)
        {
            _itemSplitDragSlots.Clear();
            
            
            _isItemSplitDragging = true;
            _itemSplitDragSlots.Add(new ItemSplitDragSlot(startSlot,_playerInventoryModel[startSlot]));
            _dragStartEquippedItem = startEquippedItem;
        }
        

        public void ItemSplitDragSlot(int slot)
        {
            if (!_playerInventoryModel[slot].IsAllowedToAddWithRemain(_equippedItem) && _isItemSplitDragging) return;

            
            //まだスロットをドラッグしてない時
            if (!_itemSplitDragSlots.Exists(i => i.Slot == slot))
            {
                //ドラッグ中のアイテムに設定
                _itemSplitDragSlots.Add(new ItemSplitDragSlot(slot,_playerInventoryModel[slot]));
            }

            var id = _dragStartEquippedItem.Id;
            
            //1スロットあたりのアイテム数
            var dragItemCount = _dragStartEquippedItem.Count/_itemSplitDragSlots.Count;
            //余っているアイテム数
            var remainItemNum = _dragStartEquippedItem.Count - dragItemCount*_itemSplitDragSlots.Count;
            
            foreach (var dragSlot in _itemSplitDragSlots)
            {
                //ドラッグ中のスロットにアイテムを加算する
                var addedItem = dragSlot.BeforeDragItem.AddItem(_itemStackFactory.Create(id,dragItemCount));

                SetInventoryWithInvokeEvent(dragSlot.Slot,addedItem.ProcessResultItemStack);
                //余ったアイテムを加算する
                remainItemNum += addedItem.RemainderItemStack.Count;
            }

            if (remainItemNum == 0)
            {
                //余ったアイテムがなくなったら持ち状態を解除する
                SetEquippedWithInvokeEvent(false);
            }
            else
            {
                //持っているアイテムを設定
                SetEquippedWithInvokeEvent(true,_itemStackFactory.Create(id,remainItemNum));
            }
        }
        
        public void ItemSplitDragEndSlot(int slot)
        {
            _isItemSplitDragging = false;
        }

        #endregion
        
        #region OneDrag

        private bool _isItemOneDragging;
        
        private void ItemOneDragStart()
        {
            _isItemOneDragging = true;
        }

        public void ItemOneDragEnd()
        {
            _isItemOneDragging = false;
        }

        #endregion

        public void CollectSlotItem(int slot)
        {
            //同じIDのアイテムで少ない数のスロット順で並べる
            var collectTargetIndex = GetCollectItemTarget(_playerInventoryModel[slot].Id);
            //ただし自分のスロットは除外する
            collectTargetIndex.Remove(slot);

            SetInventoryWithInvokeEvent(slot, CollectItem(collectTargetIndex, _playerInventoryModel[slot]));
        }
        public void CollectEquippedItem()
        {
            //同じIDのアイテムで少ない数のスロット順で並べる
            var collectTargetIndex = GetCollectItemTarget(_equippedItem.Id);
            
            SetEquippedWithInvokeEvent(true,CollectItem(collectTargetIndex,_equippedItem));
        }

        private List<int> GetCollectItemTarget(int itemId)
        {
            return _playerInventoryModel.
                Select((item,index) => new {item,index}).
                Where(i => i.item.Id == itemId).
                OrderBy(i => i.item.Count).
                Select(i => i.index).ToList();
        }

        private IItemStack CollectItem(List<int> collectTargetIndex,IItemStack collectFromItem)
        {
            foreach (var index in collectTargetIndex)
            {
                var added = collectFromItem.AddItem(_playerInventoryModel[index]);
                collectFromItem = added.ProcessResultItemStack;
                SetInventoryWithInvokeEvent(index,added.RemainderItemStack);
                
                //足したあまりがあるということはスロットにそれ以上入らないということなので、ここで処理を終了する
                if (added.RemainderItemStack.Count != 0)
                {
                    break;
                }
            }

            return collectFromItem;
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
            _playerInventoryModel[slot] = itemStack;
            OnSlotUpdate?.Invoke(slot,itemStack.ToStructItemStack());
        }

    }
    
    public static class ItemStackExtend{
        public static ItemStack ToStructItemStack(this  IItemStack itemStack)
        {
            return new ItemStack(itemStack.Id,itemStack.Count);
        }
    }

    class ItemSplitDragSlot
    {
        public readonly int Slot;
        public readonly IItemStack BeforeDragItem;

        public ItemSplitDragSlot(int slot,IItemStack beforeDragItem)
        {
            BeforeDragItem = beforeDragItem;
            Slot = slot;
        }
    }
}