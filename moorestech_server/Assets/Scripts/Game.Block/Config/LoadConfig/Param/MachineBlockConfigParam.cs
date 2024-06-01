using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class MachineBlockConfigParam : IBlockConfigParam
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