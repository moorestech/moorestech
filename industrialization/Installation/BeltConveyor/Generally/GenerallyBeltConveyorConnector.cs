using System;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorConnector : IBeltConveyorComponent
    {
        private readonly IInstallationInventory _connect;

        public GenerallyBeltConveyorConnector(IInstallationInventory connect)
        {
            _connect = connect;
        }

        public bool InsertItem(IItemStack item)
        {
            throw new NotImplementedException();
        }
    }
}