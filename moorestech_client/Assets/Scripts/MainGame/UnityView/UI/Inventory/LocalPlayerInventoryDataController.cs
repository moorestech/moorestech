using System.Collections;
using System.Collections.Generic;
using Core.Item;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace MainGame.UnityView.UI.Inventory
{
    //名前変えたい
    public class LocalPlayerInventoryDataController
    {
        public readonly InventoryItems AllInventoryItems;
        public IItemStack GrabInventory { get; private set; }

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count)
        {
            
        }

        public void DirectMoveBetweenInventory(int moveFromSlot)
        {
            
        }
        
        public void SetGrabItem(IItemStack itemStack)
        {
            
        }
        
        
        public LocalPlayerInventoryDataController()
        {
            AllInventoryItems = new InventoryItems();
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