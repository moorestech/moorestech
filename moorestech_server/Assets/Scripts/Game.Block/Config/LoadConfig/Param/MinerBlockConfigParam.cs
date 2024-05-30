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
        public readonly int ItemId;
        public readonly int MiningTime;
        
        public MineItemSetting(int miningTime, int itemId)
        {
            MiningTime = miningTime;
            ItemId = itemId;
        }
    }
}