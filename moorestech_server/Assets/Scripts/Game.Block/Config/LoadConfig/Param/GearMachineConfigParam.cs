using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Config.LoadConfig.OptionLoader;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearMachineConfigParam : IBlockConfigParam
    {
        public readonly int InputSlot;
        public readonly int OutputSlot;
        
        public readonly int RequiredRpm;
        public readonly float RequiredTorque;
        public readonly int TeethCount;
        
        
        public List<ConnectSettings> GearConnectSettings;
        
        private GearMachineConfigParam(dynamic blockParam, IItemConfig itemConfig)
        {
            InputSlot = blockParam.inputSlot;
            OutputSlot = blockParam.outputSlot;
            TeethCount = blockParam.teethCount;
            RequiredRpm = blockParam.requiredRpm;
            RequiredTorque = blockParam.requiredTorque;
            
            GearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader);
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            return new GearMachineConfigParam(blockParam, itemConfig);
        }
    }
}