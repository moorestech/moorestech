using System;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachine : InstallationBase,IInstallationInventory
    {
        public readonly NormalMachineInputInventory NormalMachineInputInventory;

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return NormalMachineInputInventory.InsertItem(itemStack);
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            NormalMachineInputInventory.
                NormalMachineStartProcess.
                NormalMachineRunProcess.
                NormalMachineOutputInventory
                .ChangeConnectInventory(installationInventory);
        }

        public NormalMachine(int installationId, int intID,NormalMachineInputInventory normalMachineInputInventory) : base(installationId, intID)
        {
            
            NormalMachineInputInventory = normalMachineInputInventory;
            intID = intID;
            InstallationID = installationId;
        }
    }
}