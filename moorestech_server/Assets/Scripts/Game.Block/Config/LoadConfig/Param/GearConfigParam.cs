using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearConfigParam : IBlockConfigParam
    {
        public readonly int TeethCount;
        public readonly float RequiredPower;

        public List<ConnectSettings> GearConnectSettings;

        private GearConfigParam(int teethCount, float requiredPower, List<ConnectSettings> gearConnectSettings)
        {
            TeethCount = teethCount;
            GearConnectSettings = gearConnectSettings;
            RequiredPower = requiredPower;
        }

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float lossPower = blockParam.lossPower;

            var gearConnectors = blockParam.gearConnectors;
            var gearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects");

            return new GearConfigParam(teethCount, lossPower, gearConnectSettings);
        }
    }
}