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
    public class GearBeltConveyorComponent : GearEnergyTransformer
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _timeOfItemEnterToExit;
        private readonly float _idleTorqueRate;
        private readonly GearConsumption _consumption;

        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double timeOfItemEnterToExit, GearConsumption gearConsumption, BlockConnectorComponent<IGearEnergyTransformer, GearContext> blockConnectorComponent)
            : base(gearConsumption, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            _idleTorqueRate = gearConsumption.IdlePowerRate;
            _consumption = gearConsumption;

            _beltConveyorComponent.OnItemsChanged.Subscribe(_ => UpdateTorqueRequestRate());
            OnChangeBlockState.Subscribe(_ => UpdateSpeed());
            UpdateTorqueRequestRate();

            #region Internal
            // 軸の実RPMから速度を求め、空ベルトの要求トルク倍率を掛けない。
            // Derive speed from actual shaft RPM without multiplying the empty-belt torque request rate.
            void UpdateSpeed()
            {
                BlockException.CheckDestroy(this);

                // 稼働率0（停止・RPM不足）なら搬送を止める
                // Stop transport when the operating rate is zero (stopped or insufficient RPM)
                var rpm = CurrentRpm.AsPrimitive();
                var operatingRate = _consumption.BaseRpm <= 0 || rpm < _consumption.MinimumRpm ? 0 : rpm / _consumption.BaseRpm;
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
            #endregion
        }

        private void UpdateTorqueRequestRate()
        {
            // ベルト上のアイテム有無で要求トルク倍率を変更要求する
            // Push the torque request rate based on whether items are on the belt
            var hasItem = _beltConveyorComponent.BeltConveyorItems.Any(item => item != null);
            SetTorqueRequestRate(hasItem ? 1f : _idleTorqueRate);
        }
    }
}
