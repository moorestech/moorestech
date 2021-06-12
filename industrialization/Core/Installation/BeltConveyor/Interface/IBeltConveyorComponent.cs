using industrialization.Core.Item;

namespace industrialization.Core.Installation.BeltConveyor.Interface
{
    public interface IBeltConveyorComponent
    {
        public bool InsertItem(IItemStack item);

        public void ChangeConnector(IInstallationInventory installationInventory);
    }
}