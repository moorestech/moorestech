using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// 左ドラッグでGrabアイテムを複数スロットへ等分配置するスプリットドラッグを担う
    /// Handles split-drag: distributing the grabbed stack evenly across dragged slots on left-drag
    /// </summary>
    public class PlayerInventorySplitDragHandler
    {
        //現在スプリットドラッグしているスロットのリスト
        private readonly List<ItemSplitDragSlot> _itemSplitDraggedSlots = new();

        //ドラッグ中のアイテムをドラッグする前のGrabインベントリ
        private IItemStack _grabInventoryBeforeDrag;

        private readonly LocalPlayerInventoryController _playerInventory;

        public PlayerInventorySplitDragHandler(LocalPlayerInventoryController playerInventory)
        {
            _playerInventory = playerInventory;
        }

        // ドラッグ開始時のGrabを控えてから最初のスロットへ配分する
        // Snapshot the grab at drag start, then distribute to the first slot
        public void BeginDrag(int slotIndex)
        {
            _grabInventoryBeforeDrag = _playerInventory.GrabInventory;
            DragTo(slotIndex, false);
        }

        public void ClearDraggedSlots()
        {
            _itemSplitDraggedSlots.Clear();
        }

        public void DragTo(int slotIndex, bool isMoveSendData)
        {
            if (!_playerInventory.LocalPlayerInventory[slotIndex].IsAllowedToAddWithRemain(_playerInventory.GrabInventory)) return;

            // まだスロットをドラッグしてない時
            var doNotDragging = !_itemSplitDraggedSlots.Exists(i => i.Slot == slotIndex);
            // アイテムがない時か、同じアイテムがあるとき
            var isNotSlotOrSameItem = _playerInventory.LocalPlayerInventory[slotIndex].Id == ItemMaster.EmptyItemId || _playerInventory.LocalPlayerInventory[slotIndex].Id == _grabInventoryBeforeDrag.Id;

            // まだスロットをドラッグしてない時 か アイテムがない時か、同じアイテムがあるとき
            if (doNotDragging && isNotSlotOrSameItem)
            {
                //ドラッグ中のアイテムに設定
                _itemSplitDraggedSlots.Add(new ItemSplitDragSlot(slotIndex, _playerInventory.LocalPlayerInventory[slotIndex]));
            }

            //一度Grabインベントリをリセットする
            _playerInventory.SetGrabItem(_grabInventoryBeforeDrag);
            foreach (var itemSplit in _itemSplitDraggedSlots) _playerInventory.SetMainItem(itemSplit.Slot, itemSplit.BeforeDragItem);

            //1スロットあたりのアイテム数
            var grabItem = _playerInventory.GrabInventory;
            var dragItemCount = grabItem.Count / _itemSplitDraggedSlots.Count;
            //余っているアイテム数
            var remainItemNum = grabItem.Count - dragItemCount * _itemSplitDraggedSlots.Count;

            var itemStackFactory = ServerContext.ItemStackFactory;

            foreach (var dragSlot in _itemSplitDraggedSlots)
            {
                //ドラッグ中のスロットにアイテムを加算する
                var addedItem = dragSlot.BeforeDragItem.AddItem(itemStackFactory.Create(grabItem.Id, dragItemCount));
                var moveItemCount = addedItem.ProcessResultItemStack.Count - dragSlot.BeforeDragItem.Count;

                _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, dragSlot.Slot, moveItemCount, isMoveSendData);
                //余ったアイテムを加算する
                remainItemNum += addedItem.RemainderItemStack.Count;
            }

            //あまりのアイテムをGrabインベントリに設定する
            _playerInventory.SetGrabItem(itemStackFactory.Create(grabItem.Id, remainItemNum));
        }
    }

    public class ItemSplitDragSlot
    {
        public ItemSplitDragSlot(int slot, IItemStack beforeDragItem)
        {
            Slot = slot;
            BeforeDragItem = beforeDragItem;
        }

        public int Slot { get; }
        public IItemStack BeforeDragItem { get; }
    }
}
