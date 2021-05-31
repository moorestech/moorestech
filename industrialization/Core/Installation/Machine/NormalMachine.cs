using System;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachine : InstallationBase,IInstallationInventory
    {
        public readonly NormalMachineInputInventory NormalMachineInputInventory;
        public NormalMachine(int installationId, Guid guid,NormalMachineInputInventory normalMachineInputInventory) : base(installationId,guid)
        {
            NormalMachineInputInventory = normalMachineInputInventory;
            GUID = guid;
            InstallationID = installationId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return NormalMachineInputInventory.InsertItem(itemStack);
        }
    }
}