using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftingInventory : IInventory
    {
        public void Craft();
    }
}