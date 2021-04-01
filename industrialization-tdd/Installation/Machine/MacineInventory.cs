using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MacineInventory : IInstallationInventory
    {
        private InventoryData inventoryData;
        
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