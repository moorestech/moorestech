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
        private readonly float _requiredTorque;
        private readonly IDisposable _updateObservable;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, float requiredTorque, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
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
        
        public override void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            base.SupplyPower(rpm, torque, isClockwise);
            var torqueRate = torque / _requiredTorque;
            var speed = torqueRate * rpm * _beltConveyorSpeed;
            _beltConveyorComponent.SetTimeOfItemEnterToExit(1 / speed);
        }
    }
}