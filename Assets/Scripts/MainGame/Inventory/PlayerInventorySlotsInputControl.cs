using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Inventory
{
    public class PlayerInventorySlotsInputControl : MonoBehaviour
    {
        [SerializeField] private List<InventoryItemSlot> mainInventorySlots;

        private PlayerInventoryModel _playerInventoryModel;

        [Inject]
        public void Construct(PlayerInventoryModel playerInventoryModel)
        {
            _playerInventoryModel = playerInventoryModel;
        }
        
        private void Awake()
        {
            foreach (var mainInventory in mainInventorySlots)
            {
                mainInventory.OnLeftClickDown += LeftClickDown;
                mainInventory.OnRightClickDown += RightClickDown;
                mainInventory.OnLeftClickUp += LeftClickUp;
                mainInventory.OnRightClickUp += RightClickUp;
                mainInventory.OnCursorEnter += CursorEnter;
                mainInventory.OnDoubleClick += DoubleClick;
            }
        }

        private void DoubleClick(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsEquipped)
            {
                _playerInventoryModel.CollectEquippedItem();
            }
            else
            {
                _playerInventoryModel.CollectSlotItem(slotIndex);
            }
        }

        private void CursorEnter(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsItemSplitDragging)
            {
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                _playerInventoryModel.ItemSplitDragSlot(slotIndex);
            }
            else if (_playerInventoryModel.IsItemOneDragging)
            {
                _playerInventoryModel.PlaceOneItem(slotIndex);
            }
        }

        private void RightClickUp(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsItemOneDragging)
            {
                _playerInventoryModel.ItemOneDragEnd();
            }
        }

        private void LeftClickUp(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_playerInventoryModel.IsItemSplitDragging)
            {
                _playerInventoryModel.ItemSplitDragEndSlot(slotIndex);
            }
        }

        private void RightClickDown(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsEquipped)
            {
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                _playerInventoryModel.PlaceOneItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理
                _playerInventoryModel.EquippedHalfItem(slotIndex);
            }
            
        }
        
        private void LeftClickDown(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsEquipped)
            {
                //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                _playerInventoryModel.PlaceItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                _playerInventoryModel.EquippedItem(slotIndex);
            }
        }
        
        
    }
}