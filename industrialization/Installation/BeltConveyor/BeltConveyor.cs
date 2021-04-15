using System;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor
{
    //TODO ベルトコンベアの仮コードを直す
    public class BeltConveyor : InstallationBase, IInstallationInventory, IBeltConveyor
    {
        private double _beltConveyorSpeed = 0;
        public BeltConveyor(int installationId, Guid guid) : base(installationId, guid)
        {
            GUID = guid;
            InstallationID = installationId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            throw new NotImplementedException();
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