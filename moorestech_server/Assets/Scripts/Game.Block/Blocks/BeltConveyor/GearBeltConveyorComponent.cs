using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Gear.Common;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : GearEnergyTransformer
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly double _beltConveyorSpeed;
        private readonly Torque _requiredTorque;
        private readonly IDisposable _updateObservable;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, Torque requiredTorque, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(requiredTorque, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredTorque = requiredTorque;
            _beltConveyorSpeed = beltConveyorSpeed;
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        private void Update()
        {
            BlockException.CheckDestroy(this);
        }
        
        public override void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);
            var torqueRate = torque / _requiredTorque;
            var speed = torqueRate.AsPrimitive() * rpm.AsPrimitive() * _beltConveyorSpeed;
            _beltConveyorComponent.SetTimeOfItemEnterToExit(1 / speed);
        }
    }
}