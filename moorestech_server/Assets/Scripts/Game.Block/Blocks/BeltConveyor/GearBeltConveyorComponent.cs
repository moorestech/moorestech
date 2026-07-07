using Core.Update;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _timeOfItemEnterToExit;
        private readonly float _idleTorqueRate;

        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double timeOfItemEnterToExit, GearConsumption gearConsumption, BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge> blockConnectorComponent)
            : base(gearConsumption, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            _idleTorqueRate = gearConsumption.IdlePowerRate;

            _beltConveyorComponent.OnItemsChanged.Subscribe(_ => UpdateTorqueRequestRate());
            UpdateTorqueRequestRate();
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
        }

        private void UpdateTorqueRequestRate()
        {
            // ベルト上のアイテム有無で要求トルク倍率を変更要求する
            // Push the torque request rate based on whether items are on the belt
            var hasItem = _beltConveyorComponent.BeltConveyorItems.Any(item => item != null);
            SetTorqueRequestRate(hasItem ? 1f : _idleTorqueRate);
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

            // 稼働率（RPM比 × torqueRate、下限未満で0）で搬送時間を割って実搬送時間を求める
            // Divide the base transit time by the operating rate (rpmRatio × torqueRate, zero below minimum)
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
