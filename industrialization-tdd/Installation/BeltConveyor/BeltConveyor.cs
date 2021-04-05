using System;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor
{
    //TODO ベルトコンベアの仮コードを直す
    public class BeltConveyor : InstallationBase, IInstallationInventory, IBeltConveyor
    {
        private InventoryData inventoryData;
        public BeltConveyor(int installationId, Guid guid) : base(installationId, guid)
        {
            inventoryData = new InventoryData(10);
            GUID = guid;
            InstallationID = installationId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            throw new NotImplementedException();
        }

        public InventoryData GetInventory()
        {
            return inventoryData;
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