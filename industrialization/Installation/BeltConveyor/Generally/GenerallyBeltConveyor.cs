using System;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyor : InstallationBase, IInstallationInventory, IBeltConveyor
    {
        private readonly IBeltConveyorItemInventory _beltConveyorItemInventory;
        private const int CanCarryItemNum = 1; 
        public IItemStack InsertItem(IItemStack itemStack)
        {
            if (_beltConveyorItemInventory.InsertItem(itemStack))
            {
                return itemStack.SubItem(CanCarryItemNum);
            }
            return itemStack;
        }

        public GenerallyBeltConveyor(int installationId, Guid guid,IBeltConveyorItemInventory beltConveyorItemInventory) : base(installationId, guid)
        {
            _beltConveyorItemInventory = beltConveyorItemInventory;
        }
    }
}