using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearConfigParam : IBlockConfigParam
    {
        public readonly Torque RequireTorque;
        public readonly int TeethCount;
        
        public List<ConnectSettings> GearConnectSettings;
        
        private GearConfigParam(int teethCount, Torque requireTorque, List<ConnectSettings> gearConnectSettings)
        {
            TeethCount = teethCount;
            GearConnectSettings = gearConnectSettings;
            RequireTorque = requireTorque;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float requireTorque = blockParam.requireTorque;
            
            var gearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, GearConnectConst.GearConnectOptionKey, GearConnectOptionLoader.Loader);
            return new GearConfigParam(teethCount, new Torque(requireTorque), gearConnectSettings);
        }
    }
}