using System;
using System.Collections.Generic;
using MainGame.Basic;

namespace MainGame.Inventory
{
    public class PlayerInventoryModel
    {
        public IReadOnlyList<ItemStack> MainInventory => _mainInventory;
        private readonly List<ItemStack> _mainInventory = new ();
        
        private ItemStack _equippedItem;

        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<int> OnDragSlot;
        public event Action<int> OnDragEndSlot;
        public event Action<ItemStack> OnEquippedItemUpdate;
        


        public PlayerInventoryModel()
        {
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                _mainInventory.Add(new ItemStack());
            }
        }



        public void EquippedItem(int slot)
        {
            
        }

        public void PlaceItem(int slot)
        {
            
        }
        

        public void PlaceOneItem(int slot)
        {
            
        }
        
        public void DragStartSlot(int slot)
        {
        }

        public void DragSlot(int slot)
        {
            
        }
        
        public void DragEndSlot(int slot)
        {
        }
        
    }
}