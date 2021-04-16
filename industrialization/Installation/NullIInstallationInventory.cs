using industrialization.Item;

namespace industrialization.Installation
{
    public class NullIInstallationInventory : IInstallationInventory
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }
    }
}