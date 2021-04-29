using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorInventory : IBeltConveyorItemInventory
    {
        private readonly IBeltConveyorConnector _beltConveyorConnector;

        public GenerallyBeltConveyorInventory(IBeltConveyorConnector beltConveyorConnector)
        {
            _beltConveyorConnector = beltConveyorConnector;
        }

        public bool InsertItem(IItemStack item)
        {
            throw new System.NotImplementedException();
        }
    }
}