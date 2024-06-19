using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.EnergySystem;

namespace Game.Block.Config.LoadConfig.Param
{
    public interface IMachineBlockParam
    {
        public int InputSlot { get; }
        public int OutputSlot { get; }
    }
    
    public class MachineBlockConfigParam : IBlockConfigParam, IMachineBlockParam
    {
        public readonly ElectricPower RequiredPower;
        
        private MachineBlockConfigParam(int inputSlot, int outputSlot, ElectricPower requiredPower)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
            RequiredPower = requiredPower;
        }
        public int InputSlot { get; }
        public int OutputSlot { get; }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int inputSlot = blockParam.inputSlot;
            int outputSlot = blockParam.outputSlot;
            float requiredPower = blockParam.requiredPower;
            return new MachineBlockConfigParam(inputSlot, outputSlot, new ElectricPower(requiredPower));
        }
    }
}