using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class PowerGeneratorConfigParam : IBlockConfigParam
    {
        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int FuelSlot;
        public readonly int InfinityPower;
        public readonly bool IsInfinityPower;

        public PowerGeneratorConfigParam(Dictionary<int, FuelSetting> fuelSettings, int fuelSlot, bool isInfinityPower,
            int infinityPower)
        {
            FuelSettings = fuelSettings;
            FuelSlot = fuelSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
        }
    }

    public class FuelSetting
    {
        public readonly int ItemId;
        public readonly int Power;
        public readonly int Time;

        public FuelSetting(int itemId, int time, int power)
        {
            ItemId = itemId;
            Time = time;
            Power = power;
        }
    }
}