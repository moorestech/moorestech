using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private InventoryData inventoryData;

        public MachineInventory(int inventorySlots)
        {
            inventoryData = new InventoryData(inventorySlots);
        }

        public bool InsertItem(ItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}