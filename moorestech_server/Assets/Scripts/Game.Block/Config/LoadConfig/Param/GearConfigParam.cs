using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearConfigParam : IBlockConfigParam
    {
        public readonly int TeethCount;
        public readonly float LossPower;

        public List<ConnectSettings> InputConnectSettings;
        public List<ConnectSettings> OutputConnectSettings;

        private GearConfigParam(int teethCount, float lossPower, List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings)
        {
            TeethCount = teethCount;
            InputConnectSettings = inputConnectSettings;
            OutputConnectSettings = outputConnectSettings;
            LossPower = lossPower;
        }

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float lossPower = blockParam.lossPower;

            var gearConnectors = blockParam.gearConnectors;
            var inputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, true);
            var outputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, false);

            return new GearConfigParam(teethCount, lossPower, inputConnectSettings, outputConnectSettings);
        }
    }
}