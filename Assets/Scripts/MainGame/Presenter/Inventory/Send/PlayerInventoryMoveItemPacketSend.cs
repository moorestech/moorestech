using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.UIState;
using Server.Protocol.PacketResponse;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class PlayerInventoryMoveItemPacketSend : IInitializable
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        private readonly IBlockClickDetect _blockClickDetect;

        private InventoryType _currentSubInventoryType;
        private Vector2Int _blockPos;

        public PlayerInventoryMoveItemPacketSend(UIStateControl uiStateControl, PlayerInventoryViewModelController playerInventoryViewModelController,InventoryMoveItemProtocol inventoryMoveItem,IBlockClickDetect blockClickDetect)
        {
            uiStateControl.OnStateChanged += OnStateChanged;
            playerInventoryViewModelController.OnItemSlotGrabbed += ItemSlotGrabbed;
            playerInventoryViewModelController.OnItemSlotCollect += ItemSlotGrabbed;
            playerInventoryViewModelController.OnGrabItemReplaced += ItemSlotGrabbed;
            playerInventoryViewModelController.OnItemSlotAdded += ItemSlotAdded;
            _inventoryMoveItem = inventoryMoveItem;
            _blockClickDetect = blockClickDetect;
        }

        private void ItemSlotGrabbed(int slot, int count)
        {
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                _inventoryMoveItem.Send(true,InventoryType.MainInventory, slot, count,_blockPos.x,_blockPos.y);
            }
            else
            {
                _inventoryMoveItem.Send(true,_currentSubInventoryType, slot - PlayerInventoryConstant.MainInventorySize, count,_blockPos.x,_blockPos.y);
            }
        }

        private void ItemSlotAdded(int slot, int addCount)
        {
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                _inventoryMoveItem.Send(false,InventoryType.MainInventory, slot, addCount,_blockPos.x,_blockPos.y);
            }
            else
            {
                _inventoryMoveItem.Send(false,_currentSubInventoryType, slot - PlayerInventoryConstant.MainInventorySize, addCount,_blockPos.x,_blockPos.y);
            }
            
        }

        private void OnStateChanged(UIStateEnum state)
        {
            //今開いているサブインベントリのタイプを設定する
            _currentSubInventoryType = state switch
            {
                //プレイヤーインベントリを開いているということは、サブインベントリはCraftInventoryなのでそれを設定する
                UIStateEnum.PlayerInventory => InventoryType.CraftInventory,
                UIStateEnum.BlockInventory => InventoryType.BlockInventory,
                _ => _currentSubInventoryType
            };
            
            //ブロックだった場合のために現在の座標を取得しておく
            _blockClickDetect.TryGetPosition(out _blockPos);
        }

        public void Initialize() { }
    }
}