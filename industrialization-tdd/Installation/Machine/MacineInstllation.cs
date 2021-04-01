using System;
using industrialization.Inventory;
using industrialization.Item;
using industrialization_tdd.GameSystem;

namespace industrialization.Installation.Machine
{
    //TODO きちんと入ってきたアイテムを処理する機構を作る
    //TODO レシピを取得する
    public class MacineInstllation : InstallationBase
    {
        private MacineInventory _macineInventory;

        public MacineInventory MacineInventory => _macineInventory;

        public MacineRunProcess MacineRunProcess => _macineRunProcess;

        public int InstallationId1 => InstallationID;

        public Guid Guid1 => GUID;

        private MacineRunProcess _macineRunProcess;
        public MacineInstllation(int installationId, Guid guid, IInstallationInventory connect) : base(installationId,guid)
        {
            GUID = guid;
            InstallationID = installationId;
            _macineInventory = new MacineInventory();
            _macineRunProcess = new MacineRunProcess();
        }
    }
}