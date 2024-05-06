using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class SimpleGearGeneratorParam : IBlockConfigParam
    {
        public readonly int TeethCount;
        public readonly float GenerateRpm;
        public readonly float GenerateTorque;

        public List<ConnectSettings> InputConnectSettings;
        public List<ConnectSettings> OutputConnectSettings;

        private SimpleGearGeneratorParam(int teethCount, float generateRpm, float generateTorque, List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings)
        {
            TeethCount = teethCount;
            InputConnectSettings = inputConnectSettings;
            OutputConnectSettings = outputConnectSettings;
            GenerateRpm = generateRpm;
            GenerateTorque = generateTorque;
        }

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float generateRpm = blockParam.generateRpm;
            float generateTorque = blockParam.generateTorque;

            var gearConnectors = blockParam.gearConnectors;
            var inputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, true);
            var outputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, false);

            return new SimpleGearGeneratorParam(teethCount, generateRpm, generateTorque, inputConnectSettings, outputConnectSettings);
        }
    }
}