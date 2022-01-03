using System.Collections.Generic;

namespace Core.Block.Config.LoadConfig.Param
{
    public class MinerBlockConfigParam : BlockConfigParamBase
    {
        public readonly int RequiredPower;
        public readonly List<OreSetting> OreSettings;

        public MinerBlockConfigParam(int requiredPower, List<OreSetting> oreSettings)
        {
            RequiredPower = requiredPower;
            OreSettings = oreSettings;
        }
    }

    public class OreSetting
    {
        public readonly int OreId;
        public readonly int MiningTime;

        public OreSetting(int miningTime, int oreId)
        {
            MiningTime = miningTime;
            OreId = oreId;
        }
    }
}