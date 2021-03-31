namespace industrialization.Installation.Machine
{
    public interface IMachine
    {
        public MacineState GetState();
        public void SupplyPower(double power);
        void RunProcess();
    }
}