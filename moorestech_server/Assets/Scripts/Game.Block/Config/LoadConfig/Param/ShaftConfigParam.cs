using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class ShaftConfigParam : IBlockConfigParam
    {
        public List<ConnectSettings> InputConnectSettings;
        public List<ConnectSettings> OutputConnectSettings;

        private ShaftConfigParam(List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings)
        {
            InputConnectSettings = inputConnectSettings;
            OutputConnectSettings = outputConnectSettings;
        }

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            var gearConnectors = blockParam.gearConnectors;
            var inputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, true);
            var outputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, false);

            return new ShaftConfigParam(inputConnectSettings, outputConnectSettings);
        }
    }
}