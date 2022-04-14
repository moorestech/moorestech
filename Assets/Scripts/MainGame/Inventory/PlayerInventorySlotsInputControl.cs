using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Inventory
{
    public class PlayerInventorySlotsInputControl : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;

        private PlayerInventoryModel _playerInventoryModel;

        [Inject]
        public void Construct(PlayerInventoryModel playerInventoryModel)
        {
            _playerInventoryModel = playerInventoryModel;
        }
        
        private void Awake()
        {
            playerInventorySlots.OnLeftClickDown += LeftClickDown;
            playerInventorySlots.OnRightClickDown += RightClickDown;
            playerInventorySlots.OnLeftClickUp += LeftClickUp;
            playerInventorySlots.OnRightClickUp += RightClickUp;
            playerInventorySlots.OnCursorEnter += CursorEnter;
            playerInventorySlots.OnDoubleClick += DoubleClick;
            
        }

        private void DoubleClick(int slotIndex)
        {
            if (_playerInventoryModel.IsEquipped)
            {
                _playerInventoryModel.CollectEquippedItem();
            }
            else
            {
                _playerInventoryModel.CollectSlotItem(slotIndex);
            }
        }

        private void CursorEnter(int slotIndex)
        {
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

        private void RightClickUp(int slotIndex)
        {
            if (_playerInventoryModel.IsItemOneDragging)
            {
                _playerInventoryModel.ItemOneDragEnd();
            }
        }

        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_playerInventoryModel.IsItemSplitDragging)
            {
                _playerInventoryModel.ItemSplitDragEndSlot(slotIndex);
            }
        }

        private void RightClickDown(int slotIndex)
        {
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
        
        private void LeftClickDown(int slotIndex)
        {
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