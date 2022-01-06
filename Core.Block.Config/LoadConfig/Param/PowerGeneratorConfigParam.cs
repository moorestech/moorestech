using System.Collections.Generic;

namespace Core.Block.Config.LoadConfig.Param
{
    public class PowerGeneratorConfigParam : BlockConfigParamBase
    {
        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int FuelSlot;

        public PowerGeneratorConfigParam(Dictionary<int, FuelSetting> fuelSettings, int fuelSlot)
        {
            FuelSettings = fuelSettings;
            FuelSlot = fuelSlot;
        }
    }

    public class FuelSetting
    {
        public readonly int ItemId;
        public readonly int Time;
        public readonly int Power;

        public FuelSetting(int itemId, int time, int power)
        {
            ItemId = itemId;
            Time = time;
            Power = power;
        }
    }
}