using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    public class GearBeltConveyorComponent : IBlockComponent, IGearEnergyTransformer
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
        public int EntityId { get; }
        public float RequiredPower { get; }
        public bool IsRocked { get; }
        public float CurrentRpm { get; }
        public float CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }
        public IReadOnlyList<GearConnect> Connects { get; }
        public void Rocked()
        {
            throw new NotImplementedException();
        }
        public void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            throw new NotImplementedException();
        }
        
        private void Update()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
        }
    }
}