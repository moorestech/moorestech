using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public GearComponent(GearConfigParam gearConfigParam, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(gearConfigParam.RequireTorque, blockInstanceId, connectorComponent)
        {
            TeethCount = gearConfigParam.TeethCount;
        }
        
        public GearComponent(int teethCount, Torque requiredPower, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(requiredPower, blockInstanceId, connectorComponent)
        {
            TeethCount = teethCount;
        }
        public int TeethCount { get; }
    }
}