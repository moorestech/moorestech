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
        private readonly BlockConnectorComponent<IGearEnergyTransformer> _blockConnectorComponent;
        private readonly float _requiredTorque;
        private readonly IDisposable _updateObservable;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, BlockInstanceId entityId, float requiredTorque, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(requiredTorque, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredTorque = requiredTorque;
            _blockConnectorComponent = blockConnectorComponent;
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        private void Update()
        {
            BlockException.CheckDestroy(this);
        }
    }
}