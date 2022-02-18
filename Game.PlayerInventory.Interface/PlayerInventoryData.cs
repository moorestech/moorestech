using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IInventory MainInventory;
        public readonly ICraftInventory CraftInventory;

        public PlayerInventoryData(IInventory mainInventory, ICraftInventory craftInventory)
        {
            MainInventory = mainInventory;
            CraftInventory = craftInventory;
        }
    }
}