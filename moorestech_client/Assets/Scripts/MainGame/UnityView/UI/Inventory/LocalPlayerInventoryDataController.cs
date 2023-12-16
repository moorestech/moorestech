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

            if (fromInvItem.Count < count)
            {
                return;
            }

            SetInventory();

            if (isMoveSendData)
            {
                SendMoveItemData();
            }

            #region InternalMethod
            void SetInventory()
            {
                var toInvItem = to switch
                {
                    LocalMoveInventoryType.MainOrSub => InventoryItems[toSlot],
                    LocalMoveInventoryType.Grab => GrabInventory,
                    _ => throw new ArgumentOutOfRangeException(nameof(to), to, null)
                };
                var moveItem = _itemStackFactory.Create(fromInvItem.Id, count);
                
                var add = toInvItem.AddItem(moveItem);
                switch (to)
                {
                    case LocalMoveInventoryType.MainOrSub:
                        _mainAndSubCombineItems[toSlot] = add.ProcessResultItemStack;
                        break;
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = add.ProcessResultItemStack;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(to), to, null);
                }

                var fromItemCount = fromInvItem.Count - count + add.RemainderItemStack.Count;
                var fromItem = _itemStackFactory.Create(fromInvItem.Id, fromItemCount);
                switch (from)
                {
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = fromItem;
                        break;
                    default:
                        _mainAndSubCombineItems[fromSlot] = fromItem;
                        break;
                }
            }
            void SendMoveItemData()
            {
                var fromInfo = GetServerInventoryInfo(from,fromSlot);
                var toInfo = GetServerInventoryInfo(to,toSlot);
                _inventoryMoveItemProtocol.Send(count, ItemMoveType.SwapSlot, fromInfo,fromSlot, toInfo,toSlot);
            }

            ItemMoveInventoryInfo GetServerInventoryInfo(LocalMoveInventoryType localType,int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < PlayerInventoryConst.MainInventorySize ? new ItemMoveInventoryInfo(ItemMoveInventoryType.MainInventory) : _subInventory.ItemMoveInventoryInfo,
                    LocalMoveInventoryType.Grab => new ItemMoveInventoryInfo(ItemMoveInventoryType.GrabInventory),
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null)
                }; 
            }
            #endregion
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
        }
        public void SetMainItem(IItemStack itemStack,int slot)
        {
            _mainAndSubCombineItems[slot] = itemStack;
        }
        
        public void SetSubInventory(ISubInventory subInventory)
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