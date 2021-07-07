using System;

namespace industrialization.Core.Installation.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int installationId,int intID, IInstallationInventory connect)
        {
            return new NormalMachine(installationId,intID,
                new NormalMachineInputInventory(installationId,
                    new NormalMachineStartProcess(installationId,
                        new NormalMachineRunProcess(
                            new NormalMachineOutputInventory(installationId,connect)))));
        }
    }
}