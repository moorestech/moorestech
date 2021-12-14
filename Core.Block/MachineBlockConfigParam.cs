namespace Core.Block
{
    public class MachineBlockConfigParam : BlockConfigParamBase
    {
        public readonly int InputSlot = 0;
        public readonly int OutputSlot = 0;

        public MachineBlockConfigParam(int inputSlot, int outputSlot)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
        }
    }
}