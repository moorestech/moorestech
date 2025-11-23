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
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, double beltConveyorSpeed, Torque requiredTorque, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent, GearOverloadConfig overloadConfig, IBlockRemover blockRemover)
            : base(requiredTorque, entityId, blockConnectorComponent, overloadConfig, blockRemover)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredTorque = requiredTorque;
            _beltConveyorSpeed = beltConveyorSpeed;
        }
        
        public override void Update()
        {
            BlockException.CheckDestroy(this);
            base.Update();
            if (IsDestroy) return;
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
