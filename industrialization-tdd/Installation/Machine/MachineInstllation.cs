using System;
using industrialization.Config;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    //TODO きちんと入ってきたアイテムを処理する機構を作る
    //TODO レシピを取得する
    public class MachineInstllation : InstallationBase
    {
        private MachineInventory _machineInventory;

        public MachineInventory MachineInventory => _machineInventory;

        public MachineRunProcess MachineRunProcess => _machineRunProcess;

        public int InstallationId1 => InstallationID;

        public Guid Guid1 => GUID;

        private MachineRunProcess _machineRunProcess;
        public MachineInstllation(int installationId, Guid guid, IInstallationInventory connect) : base(installationId,guid)
        {
            GUID = guid;
            InstallationID = installationId;
            _machineInventory = new MachineInventory(InstallationConfig.GetInstllationConfig(installationId).InventorySlot);
            _machineRunProcess = new MachineRunProcess(_machineInventory);
        }
    }
}