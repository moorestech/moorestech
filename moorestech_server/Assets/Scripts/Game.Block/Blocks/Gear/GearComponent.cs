using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearComponent : GearEnergyTransformer, IGear
    {
        public GearComponent(GearConfigParam gearConfigParam, EntityID entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
            : base(gearConfigParam.LossPower, entityId, connectorComponent)
        {
            TeethCount = gearConfigParam.TeethCount;
        }
        
        public int TeethCount { get; }
    }
}