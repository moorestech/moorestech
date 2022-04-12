using System;
using System.Collections.Generic;
using MainGame.Basic;

namespace MainGame.Inventory
{
    public class PlayerInventoryModel
    {
        public IReadOnlyList<ItemStack> MainInventory => _mainInventory;
        private readonly List<ItemStack> _mainInventory = new ();
        
        public bool IsEquipped => _isEquipped;
        private bool _isEquipped = false;

        private ItemStack _equippedItem;
        

        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<int> OnDragSlot;
        public event Action<int> OnDragEndSlot;
        public event Action<ItemStack> OnEquippedItemUpdate;
        public event Action OnItemEquipped;
        public event Action OnItemUnequipped;
        


        public PlayerInventoryModel()
        {
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                _mainInventory.Add(new ItemStack());
            }
        }



        public void EquippedItem(int slot)
        {
            _equippedItem = _mainInventory[slot];
            _mainInventory[slot] = new ItemStack();
            OnItemEquipped?.Invoke();
            OnEquippedItemUpdate?.Invoke(_equippedItem);
            OnSlotUpdate?.Invoke(slot,_mainInventory[slot]);
                
            _isEquipped = true;
        }

        public void PlaceItem(int slot)
        {
            _mainInventory[slot] = _equippedItem;
            OnItemUnequipped?.Invoke();
            OnSlotUpdate?.Invoke(slot,_mainInventory[slot]);
            _isEquipped = false;
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