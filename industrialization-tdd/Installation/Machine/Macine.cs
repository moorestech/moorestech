using System;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class Macine : InstallationBase,IInstallationInventory,IMachine
    {
        private IInstallationInventory connected;
        public Macine(int installationId, Guid guid) : base(installationId, guid)
        {
        }
        public MacineState GetState()
        {
            throw new System.NotImplementedException();
        }

        public void SupplyPower(double power)
        {
            throw new System.NotImplementedException();
        }

        public void RunProcess()
        {
            throw new System.NotImplementedException();
        }

        public bool InsertItem(ItemStack itemStack)
        {
            throw new NotImplementedException();
        }

        public InventoryData GetInventory()
        {
            throw new NotImplementedException();
        }
    }
}