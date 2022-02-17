using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftingInventory : IInventory
    {
        public void Craft();
        public IItemStack GetCreatableItem();
    }
}