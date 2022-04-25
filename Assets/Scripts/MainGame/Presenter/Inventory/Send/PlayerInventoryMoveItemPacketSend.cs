using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.UIState;
using UnityEngine;

namespace MainGame.Presenter.Inventory.Send
{
    public class PlayerInventoryMoveItemPacketSend
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        
        private InventoryType _currentInventoryType;
        private Vector2Int _blockPos;

        public PlayerInventoryMoveItemPacketSend(UIStateControl uiStateControl, PlayerInventoryViewModelController playerInventoryViewModelController,InventoryMoveItemProtocol inventoryMoveItem)
        {
            uiStateControl.OnStateChanged += OnStateChanged;
            playerInventoryViewModelController.OnItemGrabbed += OnItemGrabbed;
            playerInventoryViewModelController.OnItemSlotAdded += ItemSlotAdded;
            _inventoryMoveItem = inventoryMoveItem;
        }

        private void OnItemGrabbed(int slot, int count)
        {
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                _inventoryMoveItem.Send(true,InventoryType.MainInventory, slot, count,_blockPos.x,_blockPos.y);
            }
            else
            {
                _inventoryMoveItem.Send(true,_currentInventoryType, slot - PlayerInventoryConstant.MainInventorySize, count,_blockPos.x,_blockPos.y);
            }
        }

        private void ItemSlotAdded(int slot, int addCount)
        {
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                _inventoryMoveItem.Send(true,InventoryType.MainInventory, slot, addCount,_blockPos.x,_blockPos.y);
            }
            else
            {
                _inventoryMoveItem.Send(true,_currentInventoryType, slot - PlayerInventoryConstant.MainInventorySize, addCount,_blockPos.x,_blockPos.y);
            }
            
        }

        private void OnStateChanged(UIStateEnum state)
        {
            switch (state)
            {
                case UIStateEnum.PlayerInventory:
                    _currentInventoryType = InventoryType.CraftInventory;
                    break;
                case UIStateEnum.BlockInventory:
                    _currentInventoryType = InventoryType.BlockInventory;
                    break;
            }
        }
    }
}