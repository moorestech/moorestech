using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class SimpleGearGeneratorParam : IBlockConfigParam
    {
        public readonly RPM GenerateRpm;
        public readonly Torque GenerateTorque;
        public readonly int TeethCount;
        
        public List<ConnectSettings> GearConnectSettings;
        
        private SimpleGearGeneratorParam(int teethCount, RPM generateRpm, Torque generateTorque, List<ConnectSettings> gearConnectSettings)
        {
            TeethCount = teethCount;
            GearConnectSettings = gearConnectSettings;
            GenerateRpm = generateRpm;
            GenerateTorque = generateTorque;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float generateRpm = blockParam.generateRpm;
            float generateTorque = blockParam.generateTorque;
            
            var gearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, GearConnectConst.GearConnectOptionKey, GearConnectOptionLoader.Loader);
            
            return new SimpleGearGeneratorParam(teethCount, new RPM(generateRpm), new Torque(generateTorque), gearConnectSettings);
        }
    }
}