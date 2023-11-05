using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly ICraftingOpenableInventory CraftingOpenableInventory;
        public readonly IOpenableInventory GrabInventory;
        public readonly IOpenableInventory MainOpenableInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory,
            ICraftingOpenableInventory craftingOpenableInventory, IOpenableInventory grabInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            CraftingOpenableInventory = craftingOpenableInventory;
            GrabInventory = grabInventory;
        }
    }
}