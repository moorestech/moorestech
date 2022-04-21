using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftingOpenableInventory : IOpenableInventory
    {
        public void Craft();
        public void AllCraft();
        public void OneStackCraft();
        public IItemStack GetCreatableItem();
        public bool IsCreatable();
    }
}