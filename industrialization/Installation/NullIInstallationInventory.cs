using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation
{
    public class NullIInstallationInventory : IInstallationInventory
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return new NullItemStack();
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}