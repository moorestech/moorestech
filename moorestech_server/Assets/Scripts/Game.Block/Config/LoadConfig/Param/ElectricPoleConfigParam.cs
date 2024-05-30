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
    }
}