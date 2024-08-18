using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearBeltConveyorConfigParam : IBlockConfigParam
    {
        public GearBeltConveyorConfigParam(int beltConveyorItemNum, double beltConveyorSpeed, Torque requiredTorque, List<ConnectSettings> gearConnectSettings)
        {
            BeltConveyorItemNum = beltConveyorItemNum;
            BeltConveyorSpeed = beltConveyorSpeed;
            RequiredTorque = requiredTorque;
            GearConnectSettings = gearConnectSettings;
        }
        public List<ConnectSettings> GearConnectSettings { get; }
        public int BeltConveyorItemNum { get; }
        public double BeltConveyorSpeed { get; }
        public Torque RequiredTorque { get; }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int slot = blockParam.slot;
            double beltConveyorSpeed = blockParam.beltConveyorSpeed;
            float requiredTorque = blockParam.requiredTorque;
            List<ConnectSettings> connectsSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects", GearConnectOptionLoader.Loader);
            
            return new GearBeltConveyorConfigParam(slot, beltConveyorSpeed, new Torque(requiredTorque), connectsSettings);
        }
    }
}