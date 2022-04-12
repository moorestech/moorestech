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
            }
        }

        private void RightClickDown(InventoryItemSlot slot)
        {
        }
        
        private void LeftClickDown(InventoryItemSlot slot)
        {
            var slotIndex = mainInventorySlots.FindIndex(s => s == slot);
            if (_playerInventoryModel.IsEquipped)
            {
                _playerInventoryModel.PlaceItem(slotIndex);
            }
            else
            {
                _playerInventoryModel.EquippedItem(slotIndex);
            }
        }
        
        
    }
}