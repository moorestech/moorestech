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
        private readonly float _requiredPower;
        private readonly IDisposable _updateObservable;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, int entityId, float requiredPower, BlockConnectorComponent<IGearEnergyTransformer> blockConnectorComponent)
            : base(requiredPower, entityId, blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredPower = requiredPower;
            _blockConnectorComponent = blockConnectorComponent;
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        private void Update()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
        }
    }
}