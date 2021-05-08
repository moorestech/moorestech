using System;
using industrialization.Config;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class NormalMachine : InstallationBase,IInstallationInventory
    {
        private readonly IMachineComponent _machineComponent;
        public NormalMachine(int installationId, Guid guid,IMachineComponent machineComponent) : base(installationId,guid)
        {
            this._machineComponent = machineComponent;
            GUID = guid;
            InstallationID = installationId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _machineComponent.InsertItem(itemStack);
        }
    }
}