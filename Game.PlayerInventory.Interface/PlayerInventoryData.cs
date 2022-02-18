using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IInventory MainInventory;
        public readonly ICraftingInventory CraftingInventory;

        public PlayerInventoryData(IInventory mainInventory, ICraftingInventory craftingInventory)
        {
            MainInventory = mainInventory;
            CraftingInventory = craftingInventory;
        }
    }
}