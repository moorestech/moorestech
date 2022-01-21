namespace Core.Block.Config.LoadConfig.Param
{
    public class ElectricPoleConfigParam : IBlockConfigParam
    {
        public readonly int poleConnectionRange;
        public readonly int machineConnectionRange;

        public ElectricPoleConfigParam(int poleConnectionRange, int machineConnectionRange)
        {
            this.poleConnectionRange = poleConnectionRange;
            this.machineConnectionRange = machineConnectionRange;
        }
    }
}