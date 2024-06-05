using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearBeltConveyorConfigParam : IBlockConfigParam
    {
        public GearBeltConveyorConfigParam(int beltConveyorItemNum, int timeOfItemEnterToExit, float requiredPower, List<ConnectSettings> gearConnectSettings)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            RequiredPower = requiredPower;
            GearConnectSettings = gearConnectSettings;
        }
        public List<ConnectSettings> GearConnectSettings { get; }
        public int BeltConveyorItemNum { get; }
        public int TimeOfItemEnterToExit { get; }
        public float RequiredPower { get; }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            int time = blockParam.time;
            float requiredTorque = blockParam.requiredTorque;
            
            return new GearBeltConveyorConfigParam(slot, time, requiredTorque, BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader));
        }
    }
}