using System;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenericBeltConveyorConnector : IBeltConveyorComponent
    {
        private readonly IInstallationInventory _connect;

        public GenericBeltConveyorConnector(IInstallationInventory connect)
        {
            _connect = connect;
        }

        public bool InsertItem(IItemStack item)
        {
            throw new NotImplementedException();
        }
    }
}