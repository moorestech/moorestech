using System;
using System.Collections;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Util;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace MainGame.UnityView.UI.Inventory
{
    //名前変えたい
    public class LocalPlayerInventoryDataController
    {
        private readonly ItemStackFactory _itemStackFactory;
        
        public IInventoryItems InventoryItems => _mainAndSubCombineItems;
        private readonly InventoryMainAndSubCombineItems _mainAndSubCombineItems;
        
        public IItemStack GrabInventory { get; private set; }

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count,bool isMoveSendData = true)
        {
            var fromInvItem = from switch
            {
                LocalMoveInventoryType.MainOrSub => InventoryItems[fromSlot],
                LocalMoveInventoryType.Grab => GrabInventory,
                _ => throw new ArgumentOutOfRangeException(nameof(from), from, null)
            };

            if (fromInvItem.Count < count) throw new ArgumentException("移動するアイテムの数が多すぎます");
            
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
            
            
            if (from == LocalMoveInventoryType.Grab)
            {
                GrabInventory = add.RemainderItemStack;
            }else
            {
                _mainAndSubCombineItems[fromSlot] = add.RemainderItemStack;
            }
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
        }
        
        
        public LocalPlayerInventoryDataController()
        {
            _mainAndSubCombineItems = new InventoryMainAndSubCombineItems();
        }

        public void SetSubItem(ISubInventoryController subInventoryController)
        {
            
        }
    }

    public enum LocalMoveInventoryType
    {
        MainOrSub,//メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab //持ち手のインベントリ
    }
}