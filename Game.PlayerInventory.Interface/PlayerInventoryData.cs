using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory MainOpenableInventory;
        public readonly ICraftingOpenableInventory CraftingOpenableInventory;
        public readonly IOpenableInventory GrabInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, ICraftingOpenableInventory craftingOpenableInventory, IOpenableInventory grabInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            CraftingOpenableInventory = craftingOpenableInventory;
            GrabInventory = grabInventory;
        }
    }
}