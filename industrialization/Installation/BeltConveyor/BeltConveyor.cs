using System;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor
{
    //TODO ベルトコンベアの仮コードを直す
    public class BeltConveyor : InstallationBase, IInstallationInventory, IBeltConveyor
    {
        private double _beltConveyorSpeed = 0;
        private readonly InventoryData _inventoryData;
        public BeltConveyor(int installationId, Guid guid) : base(installationId, guid)
        {
            _inventoryData = new InventoryData(10);
            GUID = guid;
            InstallationID = installationId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            throw new NotImplementedException();
        }

        public InventoryData GetInventory()
        {
            return _inventoryData;
        }

        public BeltConveyorState GetState()
        {
            throw new NotImplementedException();
        }

        public void FlowItem()
        {
            throw new NotImplementedException();
        }
    }
}