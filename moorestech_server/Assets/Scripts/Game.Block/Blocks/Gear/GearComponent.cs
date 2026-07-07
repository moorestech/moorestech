using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public int TeethCount { get; }

        public GearComponent(GearBlockParam gearBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(gearBlockParam.GearConsumption, blockInstanceId, connectorComponent)
        {
            TeethCount = gearBlockParam.TeethCount;
        }

        public GearComponent(GearMachineBlockParam gearMachineBlockParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(gearMachineBlockParam.GearConsumption, blockInstanceId, connectorComponent)
        {
            TeethCount = gearMachineBlockParam.TeethCount;
        }
    }
}
