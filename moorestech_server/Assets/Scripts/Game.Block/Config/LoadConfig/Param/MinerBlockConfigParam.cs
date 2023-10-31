using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class MinerBlockConfigParam : IBlockConfigParam
    {
        public readonly List<OreSetting> OreSettings;
        public readonly int OutputSlot;
        public readonly int RequiredPower;

        public MinerBlockConfigParam(int requiredPower, List<OreSetting> oreSettings, int outputSlot)
        {
            RequiredPower = requiredPower;
            OreSettings = oreSettings;
            OutputSlot = outputSlot;
        }
    }

    public class OreSetting
    {
        public readonly int MiningTime;
        public readonly int OreId;

        public OreSetting(int oreId, int miningTime)
        {
            MiningTime = miningTime;
            OreId = oreId;
        }
    }
}