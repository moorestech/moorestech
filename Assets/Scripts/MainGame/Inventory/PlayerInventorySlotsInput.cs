using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Inventory
{
    public class PlayerInventorySlotsInput : MonoBehaviour
    {
        [SerializeField] private List<InventoryItemSlot> mainInventorySlots;

        private PlayerInventoryModel _playerInventoryModel;

        private bool _isEquippedItem;

        [Inject]
        public void Construct(PlayerInventoryModel playerInventoryModel)
        {
            _playerInventoryModel = playerInventoryModel;
        }
        
        private void Awake()
        {
            foreach (var mainInventory in mainInventorySlots)
            {
                mainInventory.OnLeftClick += OnSlotLeftClicked;
                mainInventory.OnRightClick += OnSlotRightClicked;
            }
        }

        private void OnSlotRightClicked(InventoryItemSlot slot)
        {
            
        }
        
        private void OnSlotLeftClicked(InventoryItemSlot slot)
        {
            
        }
        
        
    }
}