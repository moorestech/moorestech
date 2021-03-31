using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation
{
    public interface IInstallationInventory
    {
        public bool InsertItem(ItemStack itemStack);
        public InventoryData GetInventory();
    }
}