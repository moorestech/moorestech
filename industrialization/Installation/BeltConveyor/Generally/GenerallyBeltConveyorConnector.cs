using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorConnector : IBeltConveyorConnector
    {
        private readonly IInstallationInventory _connect;

        public GenerallyBeltConveyorConnector(IInstallationInventory connect)
        {
            _connect = connect;
        }

        public bool ConnectInsert(ItemStack item)
        {
            throw new System.NotImplementedException();
        }
    }
}