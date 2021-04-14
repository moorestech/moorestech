using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation
{
    public interface IInstallationInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
    }
}