using System;
using System.Collections.Generic;
using MainGame.Basic;
using SinglePlay;

namespace MainGame.Inventory
{
    public class PlayerInventoryModel
    {
        public IReadOnlyList<ItemStack> MainInventory => _mainInventory;
        private readonly List<ItemStack> _mainInventory = new ();
        private SinglePlayInterface _singlePlayInterface;
        
        public bool IsEquipped => _isEquipped;
        private bool _isEquipped = false;

        private ItemStack _equippedItem;


        

        public event Action<int,ItemStack> OnSlotUpdate;
        public event Action<int> OnDragSlot;
        public event Action<int> OnDragEndSlot;
        public event Action<ItemStack> OnEquippedItemUpdate;
        public event Action OnItemEquipped;
        public event Action OnItemUnequipped;
        


        public PlayerInventoryModel(SinglePlayInterface singlePlayInterface)
        {
            _singlePlayInterface = singlePlayInterface;
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
            if (_mainInventory[slot].ID == _equippedItem.ID)
            {
                _mainInventory[slot] = new ItemStack(_mainInventory[slot].ID,_mainInventory[slot].Count + 1);
                _equippedItem.Count--;
                
                OnSlotUpdate?.Invoke(slot,_mainInventory[slot]);
                OnEquippedItemUpdate?.Invoke(_equippedItem);
            }
            if (_mainInventory[slot].ID == 0)
            {
                _equippedItem.Count--;
                _mainInventory[slot] = new ItemStack(_equippedItem.ID,1);
                
                OnSlotUpdate?.Invoke(slot,_mainInventory[slot]);
                OnEquippedItemUpdate?.Invoke(_equippedItem);
            }
            
            if (_equippedItem.Count == 0)
            {
                _equippedItem = new ItemStack();
                OnItemUnequipped?.Invoke();
                _isEquipped = false;
            }
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