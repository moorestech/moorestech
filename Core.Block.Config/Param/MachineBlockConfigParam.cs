namespace Core.Block.Config.Param
{
    public class MachineBlockConfigParam : BlockConfigParamBase
    {
        public readonly int InputSlot;
        public readonly int OutputSlot;
        public readonly int RequiredPower;

        public MachineBlockConfigParam(int inputSlot, int outputSlot, int requiredPower)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
            RequiredPower = requiredPower;
        }
    }
}