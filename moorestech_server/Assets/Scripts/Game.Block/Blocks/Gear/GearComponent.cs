using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public GearComponent(GearMachineBlockParam gearMachineBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(new Torque(gearMachineBlockParam.RequireTorque), blockInstanceId, connectorComponent)
        {
            TeethCount = gearMachineBlockParam.TeethCount;
        }
        
        public GearComponent(int teethCount, Torque requiredPower, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(requiredPower, blockInstanceId, connectorComponent)
        {
            TeethCount = teethCount;
        }
        public int TeethCount { get; }
    }
}