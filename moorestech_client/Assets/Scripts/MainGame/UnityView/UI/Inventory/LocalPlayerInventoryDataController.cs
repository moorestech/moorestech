using System;
using System.Collections;
using System.Collections.Generic;
using Core.Item;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace MainGame.UnityView.UI.Inventory
{
    //名前変えたい
    public class LocalPlayerInventoryDataController
    {
        private readonly ItemStackFactory _itemStackFactory;
        
        public IInventoryItems InventoryItems => _mainAndSubCombineItems;
        private readonly InventoryMainAndSubCombineItems _mainAndSubCombineItems;
        
        
        public List<IItemStack> GrabInventory { get; private set; }

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count)
        {
            var fromInvItem = from switch
            {
                LocalMoveInventoryType.Main => InventoryItems[fromSlot],
                LocalMoveInventoryType.Grab => GrabInventory,
                _ => throw new ArgumentOutOfRangeException(nameof(from), from, null)
            };

            if (fromInvItem.Count < count) throw new ArgumentException("移動するアイテムの数が多すぎます");
            
            var moveItem = _itemStackFactory.Create(fromInvItem.Id, count);

            if (to == LocalMoveInventoryType.Main)
            {
                var add = fromInvItem.AddItem(InventoryItems[toSlot]);
                _mainAndSubCombineItems[toSlot] = add.ProcessResultItemStack;
                if (from == LocalMoveInventoryType.Grab)
                {
                    GrabInventory = add.RemainderItemStack;
                }else
                {
                    _mainAndSubCombineItems[fromSlot] = add.RemainderItemStack;
                }
            }
            else if (to == LocalMoveInventoryType.Grab)
            {
                var add = fromInvItem.AddItem(GrabInventory);
                GrabInventory = add.ProcessResultItemStack;
                if (from == LocalMoveInventoryType.Grab)
                {
                    GrabInventory = add.RemainderItemStack;
                }else
                {
                    _mainAndSubCombineItems[fromSlot] = add.RemainderItemStack;
                }
            }
        }

        public void DirectMoveBetweenInventory(int moveFromSlot)
        {
            
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            
        }
        
        
        public LocalPlayerInventoryDataController()
        {
            InventoryItems = new InventoryItems();
        }

        public void SetSubItem(ISubInventoryController subInventoryController)
        {
            
        }
    }

    public class InventoryItems : IEnumerable<IItemStack>
    {
        public IEnumerator<IItemStack> GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        public IItemStack this[int index]
        {
            get
            {
                throw new System.NotImplementedException();
            }
            
        }
    }

    public enum LocalMoveInventoryType
    {
        Main,
        Grab
    }
}