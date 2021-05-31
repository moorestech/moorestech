using System;

namespace industrialization.Core.Installation.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int installationId,Guid guid, IInstallationInventory connect)
        {
            return new NormalMachine(installationId,guid,
                new NormalMachineInputInventory(installationId,
                    new NormalMachineStartProcess(installationId,
                        new NormalMachineRunProcess(
                            new NormalMachineOutputInventory(installationId,connect)))));
        }
    }
}