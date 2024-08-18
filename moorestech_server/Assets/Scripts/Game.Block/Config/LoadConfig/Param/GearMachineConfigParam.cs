using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;
using Game.EnergySystem;
using Game.Gear.Common;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearMachineConfigParam : IBlockConfigParam, IMachineBlockParam
    {
        public readonly RPM RequiredRpm;
        public readonly Torque RequiredTorque;
        
        public readonly int TeethCount;
        
        
        public List<ConnectSettings> GearConnectSettings;
        
        private GearMachineConfigParam(dynamic blockParam, IItemConfig itemConfig)
        {
            InputSlot = blockParam.inputSlot;
            OutputSlot = blockParam.outputSlot;
            TeethCount = blockParam.teethCount;
            RequiredRpm = new RPM((float)blockParam.requiredRpm);
            RequiredTorque = new Torque((float)blockParam.requiredTorque);
            
            GearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, GearConnectConst.GearConnectOptionKey, GearConnectOptionLoader.Loader);
        }
        
        public ElectricPower RequiredPower => new(RequiredRpm.AsPrimitive() * RequiredTorque.AsPrimitive());
        public int InputSlot { get; }
        public int OutputSlot { get; }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            return new GearMachineConfigParam(blockParam, itemConfig);
        }
    }
}