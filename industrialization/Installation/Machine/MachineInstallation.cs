using System;
using industrialization.Config;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineInstallation : InstallationBase,IMachine
    {
        public MachineInventory MachineInventory { get; }

        public MachineInstallation(int installationId, Guid guid, IInstallationInventory connect) : base(installationId,guid)
        {
            GUID = guid;
            InstallationID = installationId;
            MachineInventory = new MachineInventory(installationId,connect);
        }

        public MachineState GetState()
        {
            throw new NotImplementedException();
        }

        public void SupplyPower(double power)
        {
            throw new NotImplementedException();
        }
    }
}