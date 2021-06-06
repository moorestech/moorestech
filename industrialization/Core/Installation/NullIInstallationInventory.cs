using industrialization.Core.Item;

namespace industrialization.Core.Installation
{
    public class NullIInstallationInventory : IInstallationInventory
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            
        }
    }
}