using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory MainOpenableInventory;
        public readonly ICraftingOpenableInventory CraftingOpenableInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, ICraftingOpenableInventory craftingOpenableInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            CraftingOpenableInventory = craftingOpenableInventory;
        }
    }
}