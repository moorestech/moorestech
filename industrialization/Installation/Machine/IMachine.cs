namespace industrialization.Installation.Machine
{
    public interface IMachine
    {
        public MachineState GetState();
        public void SupplyPower(double power);
    }
}