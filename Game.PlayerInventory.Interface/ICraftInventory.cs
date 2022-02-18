using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftInventory : IInventory
    {
        public void Craft();
        public IItemStack GetCreatableItem();
        public bool IsCreatable();
    }
}