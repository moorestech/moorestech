using Core.Inventory;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventoryData
    {
        public readonly IOpenableInventory GrabInventory;
        public readonly IOpenableInventory MainOpenableInventory;

        public PlayerInventoryData(IOpenableInventory mainOpenableInventory, IOpenableInventory grabInventory)
        {
            MainOpenableInventory = mainOpenableInventory;
            GrabInventory = grabInventory;
        }
    }
}