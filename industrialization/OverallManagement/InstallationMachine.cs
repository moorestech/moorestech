using System;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine.util;

namespace industrialization.OverallManagement
{
    public class InstallationMachine
    {
        public static void Create(int id, Guid from, Guid to)
        {
            var fromInventory = WorldInstallationInventoryDatastore.GetInstallation(from);
            var machineGuid = Guid.NewGuid();
            var machine = NormalMachineFactory.Create(id,machineGuid,WorldInstallationInventoryDatastore.GetInstallation(to));
            machine.ChangeConnector(fromInventory);
            
            WorldInstallationInventoryDatastore.AddInstallation(machine,machineGuid);
            WorldIInstallationDatastore.AddInstallation(machine);
        }
    }
}