using industrialization.Core.Installation.BeltConveyor.Interface;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.BeltConveyor.Generally
{
    public class GenericBeltConveyorConnector : IBeltConveyorComponent
    {
        private IInstallationInventory _connect;

        public GenericBeltConveyorConnector(IInstallationInventory connect)
        {
            _connect = connect;
        }

        public bool InsertItem(IItemStack item)
        {
            var inserted = _connect.InsertItem(item);
            return inserted.Id == NullItemStack.NullItemId;
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            _connect = installationInventory;
        }
    }
}