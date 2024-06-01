using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class ElectricPoleConfigParam : IBlockConfigParam
    {
        public readonly int machineConnectionRange;
        public readonly int poleConnectionRange;
        
        public ElectricPoleConfigParam(int poleConnectionRange, int machineConnectionRange)
        {
            this.poleConnectionRange = poleConnectionRange;
            this.machineConnectionRange = machineConnectionRange;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int poleConnectionRange = blockParam.poleConnectionRange;
            int machineConnectionRange = blockParam.machineConnectionRange;
            
            return new ElectricPoleConfigParam(poleConnectionRange, machineConnectionRange);
        }
    }
}