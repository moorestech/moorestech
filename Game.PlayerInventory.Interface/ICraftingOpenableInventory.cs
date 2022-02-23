using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftingOpenableInventory : IOpenableInventory
    {
        public void Craft();
        public IItemStack GetCreatableItem();
        public bool IsCreatable();
    }
}