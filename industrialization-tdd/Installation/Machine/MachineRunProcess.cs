namespace industrialization.Installation.Machine
{
    public class MachineRunProcess : IMachine
    {
        private MachineInventory inventory;

        public MachineRunProcess(MachineInventory inventory)
        {
            this.inventory = inventory;
        }

        public MachineState GetState()
        {
            throw new System.NotImplementedException();
        }

        public void SupplyPower(double power)
        {
            throw new System.NotImplementedException();
        }
    }
}