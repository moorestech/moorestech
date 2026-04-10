using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _beltConveyorSpeed;
        private readonly Torque _requireTorquePerRpm;

        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, Torque requireTorquePerRpm, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(new Torque(0), entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requireTorquePerRpm = requireTorquePerRpm;
            _beltConveyorSpeed = beltConveyorSpeed;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
        }

        // RPMに応じた要求トルクを計算する
        // Calculate required torque based on RPM
        public override Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            return new Torque(rpm.AsPrimitive() * _requireTorquePerRpm.AsPrimitive());
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

            // RPM依存の要求トルクを取得
            // Get RPM-dependent required torque
            var requiredTorque = GetRequiredTorque(rpm, isClockwise);

            // RPMが0なら要求トルクも0でゼロ除算になるため、搬送停止
            // RPM 0 means required torque is 0 causing division by zero, stop transport
            if (rpm.AsPrimitive() <= 0)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            // トルク比率とRPMから速度を計算し、tick数に変換
            // Calculate speed from torque ratio and RPM, convert to ticks
            var torqueRate = torque / requiredTorque;
            var speed = torqueRate.AsPrimitive() * rpm.AsPrimitive() * _beltConveyorSpeed;

            // 速度が0以下の場合はアイテムを搬送しない
            // When speed is zero or negative, prevent item transport
            if (speed <= 0)
            {
                _beltConveyorComponent.SetTicksOfItemEnterToExit(uint.MaxValue);
                return;
            }

            // 速度から通過秒数を計算し、tick数に変換
            // Calculate transit time from speed and convert to ticks
            var transitSeconds = 1 / speed;
            var ticks = GameUpdater.SecondsToTicks(transitSeconds);
            _beltConveyorComponent.SetTicksOfItemEnterToExit(ticks);
        }
    }
}
