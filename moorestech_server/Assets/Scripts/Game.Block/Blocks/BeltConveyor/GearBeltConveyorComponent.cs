using System;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : IBlockComponent
    {
        private readonly VanillaBeltConveyorComponent _beltConveyorComponent;
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly float _requiredTorque;
        private readonly IDisposable _updateObservable;
        
        public GearBeltConveyorComponent(VanillaBeltConveyorComponent beltConveyorComponent, float requiredTorque, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _beltConveyorComponent = beltConveyorComponent;
            _requiredTorque = requiredTorque;
            _blockConnectorComponent = blockConnectorComponent;
            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        private void Update()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
        }
    }
}