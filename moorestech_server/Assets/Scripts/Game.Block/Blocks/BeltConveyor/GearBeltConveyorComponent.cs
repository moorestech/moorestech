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

        public void Update()
        {
            BlockException.CheckDestroy(this);
            RefreshTransitTicks();
        }

        public override void StopNetwork()
        {
            base.StopNetwork();
            _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
        }

        public override void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);
            RefreshTransitTicks();
        }

        private void RefreshTransitTicks()
        {
            // GearRuntimeStateStore由来の稼働率から、このtickの搬送速度を決める。
            // Decide this tick's belt speed from the runtime-state operating rate.
            var operatingRate = GetCurrentOperatingRate();
            if (operatingRate <= 0f)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            // 稼働率で基準搬送時間を割り、vanilla belt側のtick数へ反映する。
            // Divide base transit time by operating rate and write it to vanilla belt ticks.
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
