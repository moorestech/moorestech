using System;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    //TODO きちんと入ってきたアイテムを処理する機構を作る
    //TODO レシピを取得する
    public class Macine : InstallationBase,IInstallationInventory,IMachine
    {
        private IInstallationInventory connected;
        private InventoryData inventoryData;
        private double power;
        public Macine(int installationId, Guid guid,IInstallationInventory connect) : base(installationId, guid)
        {
            GUID = guid;
            InstallationID = installationId;
            inventoryData = new InventoryData(10);
            connected = connect;
        }
        public MacineState GetState()
        {
            throw new System.NotImplementedException();
        }

        public void SupplyPower(double power)
        {
            this.power = power;
        }

        void RunProcess()
        {
            inventoryData.ItemStacks[1] = inventoryData.ItemStacks[0];
        }

        public bool InsertItem(ItemStack itemStack)
        {
            inventoryData.ItemStacks[0] = itemStack;
            RunProcess();
            
            return true;
        }

        public InventoryData GetInventory()
        {
            return inventoryData;
        }
    }
}