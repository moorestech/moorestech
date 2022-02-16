using Core.Item;
using Game.PlayerInventory.Interface;

namespace PlayerInventory
{
    public class PlayerCraftingInventoryData : ICraftingInventory
    {
        public IItemStack GetItem(int slot)
        {
            throw new System.NotImplementedException();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public void SetItem(int slot, int itemId, int count)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            throw new System.NotImplementedException();
        }

        public int GetSlotSize()
        {
            throw new System.NotImplementedException();
        }

        public void Craft()
        {
            throw new System.NotImplementedException();
        }
    }
}