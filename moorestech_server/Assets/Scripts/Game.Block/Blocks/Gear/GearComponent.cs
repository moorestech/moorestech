using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public int TeethCount { get; }
        
        public GearComponent(int teethCount, Torque requiredPower, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, IBlockRemover blockRemover, Guid blockGuid)
            : base(requiredPower, blockInstanceId, connectorComponent, blockRemover, blockGuid)
        {
            TeethCount = teethCount;
        }
        
        public GearComponent(GearMachineBlockParam gearMachineBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, IBlockRemover blockRemover, Guid blockGuid)
            : base(new Torque(gearMachineBlockParam.RequireTorque), blockInstanceId, connectorComponent, blockRemover, blockGuid)
        {
            TeethCount = gearMachineBlockParam.TeethCount;
        }
        
        public GearComponent(GearBlockParam gearBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, IBlockRemover blockRemover, Guid blockGuid)
            : base(new Torque(gearBlockParam.RequireTorque), blockInstanceId, connectorComponent, blockRemover, blockGuid)
        {
            TeethCount = gearBlockParam.TeethCount;
        }
    }
}
