using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation
{
    public class NullIInstallationInventory : IInstallationInventory
    {
        public bool InsertItem(ItemStack itemStack)
        {
            return false;
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}