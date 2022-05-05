using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using MainGame.Basic;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Control
{
    /// <summary>
    /// インベントリの操作の部分を受付し、実際にViewModelのインベントリリストを更新します
    ///
    /// いつかリファクタしたい
    /// </summary>
    public class PlayerInventoryViewModelController
    {
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;

        private readonly ItemStackFactory _itemStackFactory;
        private readonly IItemConfig _itemConfig;
        private IItemStack _grabbedItem;

        public bool IsGrabbed => _isGrabbed;
        private bool _isGrabbed;
        
        public bool IsItemSplitDragging => _isItemSplitDragging;
        public bool IsItemOneDragging => _isItemOneDragging;


        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<ItemStack> OnGrabbedItemUpdate;
        
        public event Action<int, int> OnItemSlotAdded; 
        
        
        /// <summary>
        /// int:grabしたインベントリのスロット　int:grabしたインベントリのアイテム数
        /// スロットを持った時に発生させるイベント
        /// </summary>
        public event Action<int,int> OnItemSlotGrabbed;


        /// <summary>
        /// アイテムをダブルクリックで集めた時に発生するイベント
        /// int:収集したインベントリのスロット　int:収集したアイテム数
        /// </summary>
        public event Action<int, int> OnItemSlotCollect;
        
        /// <summary>
        /// ドラッグでアイテムを持った時に発生させるイベント
        /// </summary>
        public event Action OnItemGrabbed;
        
        public event Action OnItemUnGrabbed;
        


        public PlayerInventoryViewModelController(SinglePlayInterface singlePlayInterface,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _itemStackFactory = singlePlayInterface.ItemStackFactory;
            _itemConfig = singlePlayInterface.ItemConfig;
            _playerInventoryViewModel = playerInventoryViewModel;
        }

        public void SetInventoryItem(int slot,int id,int count)
        {
            SetInventoryWithInvokeEvent(slot, _itemStackFactory.Create(id, count));
        }

        public void SetGrabInventoryItem(int id, int count)
        {
            if (id == ItemConstant.NullItemId)
            {
                SetGrabbedWithInvokeEvent(false, _itemStackFactory.Create(id, count));   
            }
            else
            {
                SetGrabbedWithInvokeEvent(true, _itemStackFactory.Create(id, count));
            }

        }
        
        
        #region GrabbedPlaceItem

        public void GrabbedItem(int slot)
        {
            //空スロットの時はアイテムを持たない
            if (_playerInventoryViewModel[slot].Id == ItemConstant.NullItemId) return;
            
            OnItemSlotGrabbed?.Invoke(slot,_playerInventoryViewModel[slot].Count);
            SetGrabbedWithInvokeEvent(true,_playerInventoryViewModel[slot]);
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.CreatEmpty());
        }
        public void GrabbedHalfItem(int slot)
        {
            //空スロットの時はアイテムを持たない
            if (_playerInventoryViewModel[slot].Id == ItemConstant.NullItemId) return;
            
            var grabbedItemNum = _playerInventoryViewModel[slot].Count/2;
            var slotItemNum = _playerInventoryViewModel[slot].Count - grabbedItemNum;
            var id = _playerInventoryViewModel[slot].Id;
            
            OnItemSlotGrabbed?.Invoke(slot,grabbedItemNum);
            SetGrabbedWithInvokeEvent(true,_itemStackFactory.Create(id,grabbedItemNum));
            SetInventoryWithInvokeEvent(slot,_itemStackFactory.Create(id,slotItemNum));
        }

        public void PlaceItem(int slot)
        {
            var item = _playerInventoryViewModel[slot];
            //アイテムを足しても余らない時はそのままおく
            if (item.IsAllowedToAdd(_grabbedItem))
            {
                ItemSplitDragStart(slot,_grabbedItem);
                var result = item.AddItem(_grabbedItem);
                SetInventoryWithInvokeEvent(slot,result.ProcessResultItemStack);
                SetGrabbedWithInvokeEvent(false);
            }
            //あまりがでて、アイテム数が最大じゃない時は加算して、あまりをGrabbedに入れる
            else if (item.IsAllowedToAddWithRemain(_grabbedItem) && item.Count != _itemConfig.GetItemConfig(item.Id).MaxStack)
            {
                ItemSplitDragStart(slot,_grabbedItem);
                var result = item.AddItem(_grabbedItem);
                SetInventoryWithInvokeEvent(slot,result.ProcessResultItemStack);
                SetGrabbedWithInvokeEvent(true,result.RemainderItemStack);
            }
            //加算できない時か最大数がスロットにある時はアイテムを入れ替える
            else
            {
                SetInventoryWithInvokeEvent(slot,_grabbedItem);
                SetGrabbedWithInvokeEvent(true);
                OnItemSlotAdded?.Invoke(slot,_grabbedItem.Count);
            }
        }
        

        public void PlaceOneItem(int slot)
        {
            var addItem = _itemStackFactory.Create(_grabbedItem.Id, 1);
            if (!_playerInventoryViewModel[slot].IsAllowedToAdd(addItem)) return;
            //アイテムを1個置ける時だけアイテムをおく
            
            
            //アイテムを加算する
            SetInventoryWithInvokeEvent(slot,_playerInventoryViewModel[slot].AddItem(addItem).ProcessResultItemStack);
                
            //持っているアイテムを減らす
            var newGrabbedItem = _grabbedItem.SubItem(1);
            if (newGrabbedItem.Count == 0)
            {
                //持っているアイテムがなくなったら持ち状態を解除する
                SetGrabbedWithInvokeEvent(false);
            }
            else
            {
                //なくなってない時は持っているアイテムを加算する
                ItemOneDragStart();
                SetGrabbedWithInvokeEvent(true,newGrabbedItem);
                OnItemSlotAdded?.Invoke(slot,1);
            }
        }

        #endregion

        #region SplitDrag

        private bool _isItemSplitDragging;
        private readonly List<ItemSplitDragSlot> _itemSplitDragSlots = new ();
        private IItemStack _dragStartGrabbedItem;

        private void ItemSplitDragStart(int startSlot,IItemStack startGrabbedItem)
        {
            _itemSplitDragSlots.Clear();
            
            
            _isItemSplitDragging = true;
            _itemSplitDragSlots.Add(new ItemSplitDragSlot(startSlot,_playerInventoryViewModel[startSlot]));
            _dragStartGrabbedItem = startGrabbedItem;
        }
        

        public void ItemSplitDragSlot(int slot)
        {
            if (!_playerInventoryViewModel[slot].IsAllowedToAddWithRemain(_grabbedItem) && _isItemSplitDragging) return;

            
            //まだスロットをドラッグしてない時
            if (!_itemSplitDragSlots.Exists(i => i.Slot == slot))
            {
                //ドラッグ中のアイテムに設定
                _itemSplitDragSlots.Add(new ItemSplitDragSlot(slot,_playerInventoryViewModel[slot]));
            }

            var id = _dragStartGrabbedItem.Id;
            
            //1スロットあたりのアイテム数
            var dragItemCount = _dragStartGrabbedItem.Count/_itemSplitDragSlots.Count;
            //余っているアイテム数
            var remainItemNum = _dragStartGrabbedItem.Count - dragItemCount*_itemSplitDragSlots.Count;
            
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
                SetGrabbedWithInvokeEvent(false);
            }
            else
            {
                //持っているアイテムを設定
                SetGrabbedWithInvokeEvent(true,_itemStackFactory.Create(id,remainItemNum));
            }
        }
        
        public void ItemSplitDragEndSlot(int slot)
        {
            _isItemSplitDragging = false;
            var dragItemCount = _dragStartGrabbedItem.Count/_itemSplitDragSlots.Count;
            
            //通常通り一個だけアイテムが置かれた場合でもDragEndとして扱われるのでアイテムを置いた時のイベントはDragEndで発火する
            foreach (var slots in _itemSplitDragSlots)
            {
                OnItemSlotAdded?.Invoke(slots.Slot,dragItemCount);
            }
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
            var collectTargetIndex = GetCollectItemTarget(_playerInventoryViewModel[slot].Id);
            //ただし自分のスロットは除外する
            collectTargetIndex.Remove(slot);

            SetInventoryWithInvokeEvent(slot, CollectItemWithInvokeEvent(collectTargetIndex, _playerInventoryViewModel[slot]));
        }
        public void CollectGrabbedItem()
        {
            //同じIDのアイテムで少ない数のスロット順で並べる
            var collectTargetIndex = GetCollectItemTarget(_grabbedItem.Id);
            
            SetGrabbedWithInvokeEvent(true,CollectItemWithInvokeEvent(collectTargetIndex,_grabbedItem));
        }

        private List<int> GetCollectItemTarget(int itemId)
        {
            return _playerInventoryViewModel.
                Select((item,index) => new {item,index}).
                Where(i => i.item.Id == itemId).
                OrderBy(i => i.item.Count).
                Select(i => i.index).ToList();
        }

        private IItemStack CollectItemWithInvokeEvent(List<int> collectTargetIndex,IItemStack collectFromItem)
        {
            foreach (var index in collectTargetIndex)
            {
                var added = collectFromItem.AddItem(_playerInventoryViewModel[index]);
                
                //calc collect item count
                var collectItemCount = _playerInventoryViewModel[index].Count - added.RemainderItemStack.Count;
                OnItemSlotCollect?.Invoke(index,collectItemCount);
                
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
        
        
        
        
        
        
        
        
        
        
        private void SetGrabbedWithInvokeEvent(bool isGrabbed,IItemStack itemStack = null)
        {
            _grabbedItem = itemStack ?? _itemStackFactory.CreatEmpty();
            _isGrabbed = isGrabbed;
            
            if (isGrabbed)
            {
                OnGrabbedItemUpdate?.Invoke(_grabbedItem.ToStructItemStack());
                OnItemGrabbed?.Invoke();
            }
            else
            {
                OnItemUnGrabbed?.Invoke();
            }
        }
        private void SetInventoryWithInvokeEvent(int slot,IItemStack itemStack)
        {
            _playerInventoryViewModel[slot] = itemStack;
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