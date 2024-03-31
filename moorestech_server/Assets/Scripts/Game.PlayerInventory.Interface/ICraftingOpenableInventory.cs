using Server.Core.Inventory;
using Server.Core.Item;

namespace Game.PlayerInventory.Interface
{
    public interface ICraftingOpenableInventory : IOpenableInventory
    {
        public void NormalCraft();
        public void AllCraft();
        public void OneStackCraft();
        public IItemStack GetCreatableItem();
        public bool IsCreatable();
    }
}