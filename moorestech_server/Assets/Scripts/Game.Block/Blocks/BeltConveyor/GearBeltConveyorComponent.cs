using System;
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
        private readonly Torque _requiredTorque;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, Torque requiredTorque, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(requiredTorque, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredTorque = requiredTorque;
            _beltConveyorSpeed = beltConveyorSpeed;
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            // ネットワーク停止中は搬送を止める
            // Stop transport while the network is stopped
            if (CurrentRpm.AsPrimitive() <= 0f || CurrentTorque.AsPrimitive() <= 0f) _beltConveyorComponent.SetTimeOfItemEnterToExit(double.PositiveInfinity);
        }
        
        public override void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);
            
            // RPM/トルクが不足している場合は搬送を停止する
            // Stop transport when RPM/torque is insufficient
            var torqueRate = torque / _requiredTorque;
            var speed = torqueRate.AsPrimitive() * rpm.AsPrimitive() * _beltConveyorSpeed;
            if (speed <= 0)
            {
                _beltConveyorComponent.SetTimeOfItemEnterToExit(double.PositiveInfinity);
                return;
            }
            
            // 有効な速度の場合のみ搬送時間を更新する
            // Update travel time only when speed is valid
            _beltConveyorComponent.SetTimeOfItemEnterToExit(1 / speed);
        }
    }
}
