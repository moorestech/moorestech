using industrialization.Core.Item;

namespace industrialization.Core.Installation
{
    public interface IInstallationInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
    }
}