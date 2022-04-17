using Core.Inventory;
using Core.Item;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory MainOpenableInventory;
        public readonly ICraftingOpenableInventory CraftingOpenableInventory;
        public readonly IOpenableInventory EquipmentInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, ICraftingOpenableInventory craftingOpenableInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            CraftingOpenableInventory = craftingOpenableInventory;
        }
    }
}