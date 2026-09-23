using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;
namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer
    {
        public GearBeltConveyorComponent(BlockInstanceId id, GearConsumption consumption,
            BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge> connector)
            : base(consumption, id, connector)
        {
            // 搬送速度はWorldが所有し、要求負荷は占有状態によらず一定。
            // The World owns transport speed; requested load is constant regardless of occupancy.
            SetTorqueRequestRate(1f);
        }
    }
}
