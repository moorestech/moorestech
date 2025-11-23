using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public int TeethCount { get; }
        
        public GearComponent(int teethCount, Torque requiredPower, GearOverloadConfig config, IBlockRemover remover, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(requiredPower, config, remover, blockInstanceId, connectorComponent)
        {
            TeethCount = teethCount;
        }
        
        public GearComponent(GearMachineBlockParam param, IBlockRemover remover, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(new Torque(param.RequireTorque), GearOverloadConfig.Create(param.Gear), remover, blockInstanceId, connectorComponent)
        {
            TeethCount = param.TeethCount;
        }
        
        public GearComponent(GearBlockParam param, IBlockRemover remover, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(new Torque(param.RequireTorque), GearOverloadConfig.Create(param.Gear), remover, blockInstanceId, connectorComponent)
        {
            TeethCount = param.TeethCount;
        }
    }
}
