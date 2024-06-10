using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearBeltConveyorConfigParam : IBlockConfigParam
    {
        public GearBeltConveyorConfigParam(int beltConveyorItemNum, double beltConveyorSpeed, float requiredTorque, List<ConnectSettings> gearConnectSettings)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            BeltConveyorSpeed = beltConveyorSpeed;
            RequiredTorque = requiredTorque;
            GearConnectSettings = gearConnectSettings;
        }
        public List<ConnectSettings> GearConnectSettings { get; }
        public int BeltConveyorItemNum { get; }
        public double BeltConveyorSpeed { get; }
        public float RequiredTorque { get; }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            int beltConveyorSpeed = blockParam.beltConveyorSpeed;
            float requiredTorque = blockParam.requiredTorque;
            
            return new GearBeltConveyorConfigParam(slot, beltConveyorSpeed, requiredTorque, BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader));
        }
    }
}