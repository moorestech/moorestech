using System.Collections.Generic;

namespace Core.Block.Config.LoadConfig.Param
{
    public class PowerGeneratorConfigParam : BlockConfigParamBase
    {
        public readonly List<FuelSetting> FuelSettings;

        public PowerGeneratorConfigParam(List<FuelSetting> fuelSettings)
        {
            FuelSettings = fuelSettings;
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