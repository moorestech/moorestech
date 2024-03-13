using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class MinerBlockConfigParam : IBlockConfigParam
    {
        public readonly List<MineItemSetting> MineItemSettings;
        public readonly int OutputSlot;
        public readonly int RequiredPower;

        public MinerBlockConfigParam(int requiredPower, List<MineItemSetting> mineItemSettings, int outputSlot)
        {
            RequiredPower = requiredPower;
            MineItemSettings = mineItemSettings;
            OutputSlot = outputSlot;
        }
    }

    public class MineItemSetting
    {
        public readonly int MiningTime;
        public readonly string ItemModId;
        public readonly string ItemName;

        public MineItemSetting(int miningTime, string itemModId, string itemName)
        {
            MiningTime = miningTime;
            ItemModId = itemModId;
            ItemName = itemName;
        }
    }
}