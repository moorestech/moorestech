using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public int TeethCount { get; }
        
        public GearComponent(GearConfigParam gearConfigParam, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(gearConfigParam.LossPower, entityId, connectorComponent)
        {
            TeethCount = gearConfigParam.TeethCount;
        }
        
        public GearComponent(int teethCount, float requiredPower, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(requiredPower, entityId, connectorComponent)
        {
            TeethCount = teethCount;
        }
    }
}