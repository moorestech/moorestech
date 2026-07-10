using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _timeOfItemEnterToExit;

        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double timeOfItemEnterToExit, GearConsumption gearConsumption, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(gearConsumption, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
        }

        // 毎tick、導出した稼働率（RPM比 × torqueRate）で搬送時間を更新する。GearTickUpdaterがnetwork状態を先に書くため導出値は最新
        // Update transit time each tick from the derived operating rate; GearTickUpdater writes network state first, so the derived value is current
        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 稼働率0（停止・RPM不足）なら搬送を止める
            // Stop transport when the operating rate is zero (stopped or insufficient RPM)
            var operatingRate = GetCurrentOperatingRate();
            if (operatingRate <= 0f)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            var transitSeconds = _timeOfItemEnterToExit / operatingRate;
            if (transitSeconds <= 0)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            var ticks = GameUpdater.SecondsToTicks(transitSeconds);
            _beltConveyorComponent.SetTicksOfItemEnterToExit(ticks);
        }
    }
}
