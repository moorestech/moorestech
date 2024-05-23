using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearConfigParam : IBlockConfigParam
    {
        public readonly float LossPower;
        public readonly int TeethCount;
        
        public List<ConnectSettings> GearConnectSettings;
        
        private GearConfigParam(int teethCount, float lossPower, List<ConnectSettings> gearConnectSettings)
        {
            TeethCount = teethCount;
            GearConnectSettings = gearConnectSettings;
            LossPower = lossPower;
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int teethCount = blockParam.teethCount;
            float lossPower = blockParam.lossPower;
            
            var gearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader);
            return new GearConfigParam(teethCount, lossPower, gearConnectSettings);
        }
    }
}