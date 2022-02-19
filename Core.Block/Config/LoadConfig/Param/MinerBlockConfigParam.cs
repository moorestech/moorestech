using System.Collections.Generic;

namespace Core.Block.Config.LoadConfig.Param
{
    public class MinerBlockConfigParam : IBlockConfigParam
    {
        public readonly int RequiredPower;
        public readonly int OutputSlot;
        public readonly List<OreSetting> OreSettings;

        public MinerBlockConfigParam(int requiredPower, List<OreSetting> oreSettings, int outputSlot)
        {
            RequiredPower = requiredPower;
            OreSettings = oreSettings;
            OutputSlot = outputSlot;
        }
    }

    public class OreSetting
    {
        public readonly int OreId;
        public readonly int MiningTime;

        public OreSetting(int oreId, int miningTime)
        {
            MiningTime = miningTime;
            OreId = oreId;
        }
    }
}