using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _beltConveyorSpeed;

        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, GearConsumption gearConsumption, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(gearConsumption, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _beltConveyorSpeed = beltConveyorSpeed;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
        }

        public override void StopNetwork()
        {
            base.StopNetwork();
            // ネットワーク停止時はアイテムを搬送しない
            // Prevent item transport when network is stopped
            _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
        }

        public override void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);

            // 稼働率（RPM比 × torqueRate、下限未満で0）を速度に乗じる
            // Apply operating rate (rpmRatio × torqueRate, zero below minimum) to base speed
            var operatingRate = CurrentOperatingRate;
            if (operatingRate <= 0f)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            var speed = _beltConveyorSpeed * operatingRate;
            if (speed <= 0)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            var transitSeconds = 1 / speed;
            var ticks = GameUpdater.SecondsToTicks(transitSeconds);
            _beltConveyorComponent.SetTicksOfItemEnterToExit(ticks);
        }
    }
}
