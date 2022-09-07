using MainGame.Basic;
using MainGame.Network.Send;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.UIState;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUitl;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class PlayerInventoryMoveItemPacketSend : IInitializable
    {
        private readonly InventoryMoveItemProtocol _inventoryMoveItem;
        private readonly IBlockClickDetect _blockClickDetect;

        private ItemMoveInventoryType  _currentSubInventoryType;
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

        
        /// <summary>
        /// アイテムをクリックしてもつ時に発火する
        /// </summary>
        private void ItemSlotGrabbed(int slot, int count)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
            }
            else
            {
                //サブインベントリに置く
                slot -= PlayerInventoryConstant.MainInventorySize; 
                from = new FromItemMoveInventoryInfo(_currentSubInventoryType, slot,_blockPos.x,_blockPos.y);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
            }
            _inventoryMoveItem.Send(count,ItemMoveType.SwapSlot,from,to);
        }

        /// <summary>
        /// 持っているスロットからインベントリにおいた時に発火する
        /// </summary>
        private void ItemSlotAdded(int slot, int addCount)
        {
            FromItemMoveInventoryInfo from;
            ToItemMoveInventoryInfo to;
            //スロット番号はメインインベントリから始まり、サブインベントリがメインインベントリの最後+1から始まるのでこのifが必要
            if (slot < PlayerInventoryConstant.MainInventorySize)
            {
                //メインインベントリに置く
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
                to = new ToItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory, slot);
            }
            else
            {
                //サブインベントリに置く
                slot -= PlayerInventoryConstant.MainInventorySize;
                from = new FromItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory, 0);
                to = new ToItemMoveInventoryInfo(_currentSubInventoryType, slot,_blockPos.x,_blockPos.y);
            }
            _inventoryMoveItem.Send(addCount,ItemMoveType.SwapSlot,from,to);
        }

        private void OnStateChanged(UIStateEnum state)
        {
            //今開いているサブインベントリのタイプを設定する
            _currentSubInventoryType = state switch
            {
                //プレイヤーインベントリを開いているということは、サブインベントリはCraftInventoryなのでそれを設定する
                UIStateEnum.PlayerInventory => ItemMoveInventoryType.CraftInventory,
                UIStateEnum.BlockInventory => ItemMoveInventoryType.BlockInventory,
                _ => _currentSubInventoryType
            };
            
            //ブロックだった場合のために現在の座標を取得しておく
            _blockClickDetect.TryGetCursorOnBlockPosition(out _blockPos);
        }

        public void Initialize() { }
    }
}