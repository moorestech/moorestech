using System;
using System.Collections;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using MainGame.Network.Send;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory
{
    //TODO 名前変えたい
    public class LocalPlayerInventoryDataController
    {
        public IInventoryItems InventoryItems => _mainAndSubCombineItems;
        private readonly InventoryMainAndSubCombineItems _mainAndSubCombineItems;
        public IItemStack GrabInventory { get; private set; }
        
        
        private readonly ItemStackFactory _itemStackFactory;
        private readonly InventoryMoveItemProtocol _inventoryMoveItemProtocol;
        private ISubInventory _subInventory;
        
        public LocalPlayerInventoryDataController(SinglePlayInterface singlePlayInterface,InventoryMoveItemProtocol inventoryMoveItemProtocol,IInventoryItems inventoryMainAndSubCombineItems)
        {
            _mainAndSubCombineItems = (InventoryMainAndSubCombineItems)inventoryMainAndSubCombineItems;
            _itemStackFactory = singlePlayInterface.ItemStackFactory;
            _inventoryMoveItemProtocol = inventoryMoveItemProtocol;
        }

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count,bool isMoveSendData = true)
        {
            var fromInvItem = from switch
            {
                LocalMoveInventoryType.MainOrSub => InventoryItems[fromSlot],
                LocalMoveInventoryType.Grab => GrabInventory,
                _ => throw new ArgumentOutOfRangeException(nameof(from), from, null)
            };

            if (fromInvItem.Count < count) throw new ArgumentException($"移動するアイテムの数が多すぎます from:{from} fromSlot:{fromSlot} to:{to} toSlot:{toSlot} count:{count} fromInvItem.Count:{fromInvItem.Count}");

            SetInventory();

            if (isMoveSendData)
            {
                SendMoveItemData();
            }

            #region InternalMethod
            void SetInventory()
            {
                var moveItem = _itemStackFactory.Create(fromInvItem.Id, count);

                ItemProcessResult add;
                switch (to)
                {
                    case LocalMoveInventoryType.MainOrSub:
                        add = fromInvItem.AddItem(moveItem);
                        _mainAndSubCombineItems[toSlot] = add.ProcessResultItemStack;
                        break;
                    case LocalMoveInventoryType.Grab:
                        add = fromInvItem.AddItem(moveItem);
                        GrabInventory = add.ProcessResultItemStack;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(to), to, null);
                }
            
                switch (from)
                {
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = add.RemainderItemStack;
                        break;
                    default:
                        _mainAndSubCombineItems[fromSlot] = add.RemainderItemStack;
                        break;
                }
            }
            void SendMoveItemData()
            {
                ItemMoveInventoryInfo fromInfo;
                ItemMoveInventoryInfo toInfo;
                if (from == LocalMoveInventoryType.Grab)
                {
                    fromInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory);
                    toInfo = toSlot < PlayerInventoryConst.MainInventorySize ? new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory) : _subInventory.ItemMoveInventoryInfo;
                }
                else
                {
                    fromInfo = fromSlot < PlayerInventoryConst.MainInventorySize ? new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory) : _subInventory.ItemMoveInventoryInfo;
                    toInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory);
                }
            
                Debug.Log($"from {from} fromSlot {fromSlot} to {to} toSlot {toSlot} count {count}");
                _inventoryMoveItemProtocol.Send(count, ItemMoveType.SwapSlot, fromInfo,fromSlot, toInfo,toSlot);
            }
            #endregion
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
        }
        public void SetSubItem(ISubInventory subInventory)
        {
            _mainAndSubCombineItems.SetSubInventory(subInventory.SubInventory);
            _subInventory = subInventory;
        }



    }

    public enum LocalMoveInventoryType
    {
        MainOrSub,//メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab //持ち手のインベントリ
    }
}