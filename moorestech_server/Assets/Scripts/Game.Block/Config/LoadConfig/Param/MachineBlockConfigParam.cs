using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public interface IMachineBlockParam
    {
        public int InputSlot { get; }
        public int OutputSlot { get; }
    }
    
    public class MachineBlockConfigParam : IBlockConfigParam, IMachineBlockParam
    {
        public int InputSlot { get; }
        public int OutputSlot { get; }
        public readonly int RequiredPower;
        
        private MachineBlockConfigParam(int inputSlot, int outputSlot, int requiredPower)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
            RequiredPower = requiredPower;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int inputSlot = blockParam.inputSlot;
            int outputSlot = blockParam.outputSlot;
            int requiredPower = blockParam.requiredPower;
            return new MachineBlockConfigParam(inputSlot, outputSlot, requiredPower);
        }
    }
}