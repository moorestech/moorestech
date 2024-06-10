using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearBeltConveyorConfigParam : IBlockConfigParam
    {
        public GearBeltConveyorConfigParam(int beltConveyorItemNum, int timeOfItemEnterToExit, float requiredTorque, List<ConnectSettings> gearConnectSettings)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            RequiredTorque = requiredTorque;
            GearConnectSettings = gearConnectSettings;
        }
        public List<ConnectSettings> GearConnectSettings { get; }
        public int BeltConveyorItemNum { get; }
        public int TimeOfItemEnterToExit { get; }
        public float RequiredTorque { get; }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            int time = blockParam.time;
            float requiredTorque = blockParam.requiredTorque;
            
            return new GearBeltConveyorConfigParam(slot, time, requiredTorque, BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader));
        }
    }
}