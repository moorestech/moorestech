using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Input;
using Core.Master;
using Game.Context;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// スロットのポインタイベントを解釈し、クリック/ドラッグ操作をコントローラ操作へ変換する
    /// Interprets slot pointer events and turns click/drag gestures into controller operations
    /// </summary>
    public class PlayerInventorySlotInteraction
    {
        private readonly LocalPlayerInventoryController _playerInventory;
        private readonly IReadOnlyList<ItemSlotView> _mainInventorySlotObjects;
        private readonly PlayerInventorySplitDragHandler _splitHandler;
        private readonly PlayerInventoryDirectMover _directMover;

        private ISubInventory _subInventory;
        private bool _isItemOneDragging;
        private bool _isItemSplitDragging;

        private bool IsGrabItem => _playerInventory.GrabInventory.Id != ItemMaster.EmptyItemId;

        public PlayerInventorySlotInteraction(LocalPlayerInventoryController playerInventory, IReadOnlyList<ItemSlotView> mainInventorySlotObjects)
        {
            _playerInventory = playerInventory;
            _mainInventorySlotObjects = mainInventorySlotObjects;
            _splitHandler = new PlayerInventorySplitDragHandler(playerInventory);
            _directMover = new PlayerInventoryDirectMover(playerInventory);
        }

        public void SetSubInventory(ISubInventory subInventory)
        {
            _subInventory = subInventory;
        }

        public void HandleSlotEvent((ItemSlotView slotObject, ItemUIEventType itemUIEvent) eventProperty)
        {
            var (slotObject, itemUIEvent) = eventProperty;
            var index = IndexOfMainSlotView();
            if (index == -1)
                index = _mainInventorySlotObjects.Count + _subInventory.SubInventorySlotObjects.IndexOf(slotObject);

            if (index == -1) throw new Exception("slot index not found");
            switch (itemUIEvent)
            {
                case ItemUIEventType.LeftClickDown:
                    LeftClickDown(index);
                    break;
                case ItemUIEventType.RightClickDown:
                    RightClickDown(index);
                    break;
                case ItemUIEventType.LeftClickUp:
                    LeftClickUp(index);
                    break;
                case ItemUIEventType.RightClickUp:
                    RightClickUp(index);
                    break;
                case ItemUIEventType.CursorEnter:
                    CursorEnter(index);
                    break;
                case ItemUIEventType.DoubleClick:
                    DoubleClick(index);
                    break;
                case ItemUIEventType.CursorExit: break;
                case ItemUIEventType.CursorMove: break;
                default: throw new ArgumentOutOfRangeException(nameof(itemUIEvent), itemUIEvent, null);
            }

            #region Internal

            int IndexOfMainSlotView()
            {
                // IReadOnlyListにIndexOfがないため参照一致で探索する
                // Search by reference because IReadOnlyList has no IndexOf
                for (var i = 0; i < _mainInventorySlotObjects.Count; i++)
                {
                    if (_mainInventorySlotObjects[i] == slotObject) return i;
                }

                return -1;
            }

            #endregion
        }

        private void DoubleClick(int slotIndex)
        {
            if (_isItemSplitDragging || _isItemOneDragging) return;

            // 収集本体はコントローラに集約（Web の inventory.collect と共通実装）
            // Collection itself is centralized in the controller, shared with the web's inventory.collect
            if (IsGrabItem)
                _playerInventory.CollectItems(LocalMoveInventoryType.Grab, 0);
            else
                _playerInventory.CollectItems(LocalMoveInventoryType.MainOrSub, slotIndex);
        }

        private void CursorEnter(int slotIndex)
        {
            if (_isItemSplitDragging)
                _splitHandler.DragTo(slotIndex, false);
            else if (_isItemOneDragging)
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                PlaceOneItem(slotIndex);
        }

        private void RightClickUp(int slotIndex)
        {
            if (_isItemOneDragging) _isItemOneDragging = false;
        }

        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_isItemSplitDragging)
            {
                _splitHandler.DragTo(slotIndex, true);
                _splitHandler.ClearDraggedSlots();
                _isItemSplitDragging = false;
            }
        }

        private void RightClickDown(int slotIndex)
        {
            if (IsGrabItem)
            {
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                PlaceOneItem(slotIndex);
                _isItemOneDragging = true;
            }
            else
            {
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理

                //空スロットの時はアイテムを持たない
                var item = _playerInventory.LocalPlayerInventory[slotIndex];
                if (item.Id == ItemMaster.EmptyItemId) return;

                var halfItemCount = item.Count / 2;

                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, slotIndex, LocalMoveInventoryType.Grab, 0, halfItemCount);
            }
        }

        private void LeftClickDown(int slotIndex)
        {
            if (IsGrabItem)
            {
                var isSlotEmpty = _playerInventory.LocalPlayerInventory[slotIndex].Id == ItemMaster.EmptyItemId;

                if (isSlotEmpty)
                {
                    //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                    _isItemSplitDragging = true;
                    _splitHandler.BeginDrag(slotIndex);
                }
                else
                {
                    _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, slotIndex, _playerInventory.GrabInventory.Count);
                }

                return;
            }

            if (InputManager.UI.ItemDirectMove.GetKey)
            {
                //シフト（デフォルト）＋クリックでメイン、サブのアイテム移動を直接やる処理
                _directMover.Move(slotIndex, _subInventory);
            }
            else
            {
                var slotItemCount = _playerInventory.LocalPlayerInventory[slotIndex].Count;
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, slotIndex, LocalMoveInventoryType.Grab, 0, slotItemCount);
            }
        }

        private void PlaceOneItem(int slotIndex)
        {
            var oneItem = ServerContext.ItemStackFactory.Create(_playerInventory.GrabInventory.Id, 1);
            var currentItem = _playerInventory.LocalPlayerInventory[slotIndex];

            //追加できない場合はスキップ
            if (!currentItem.IsAllowedToAdd(oneItem)) return;

            //アイテムを追加する
            _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, slotIndex, 1);

            //Grabインベントリがなくなったらドラッグを終了する
            if (_playerInventory.GrabInventory.Count == 0)
                _isItemOneDragging = false;
        }
    }
}
