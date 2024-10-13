using System;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearService
    {
        private string _currentState = IGearEnergyTransformer.WorkingStateName;
        public IObservable<BlockState> BlockStateChange => _onBlockStateChange;
        private readonly Subject<BlockState> _onBlockStateChange = new();
        
        public IObservable<GearUpdateType> OnGearUpdate => _onGearUpdate;
        private readonly Subject<GearUpdateType> _onGearUpdate = new();
        
        public RPM CurrentRpm { get; private set; }
        public Torque CurrentTorque { get; private set; }
        public bool IsCurrentClockwise { get; private set; }
        public bool IsRocked { get; private set; }
        
        public void Rocked()
        {
            IsRocked = true;
            _currentState = IGearEnergyTransformer.RockedStateName;
            CurrentRpm = new RPM(0);
            CurrentTorque = new Torque(0);
            
            var state = new BlockState(IGearEnergyTransformer.RockedStateName, _currentState);
            _onBlockStateChange.OnNext(state);
            _onGearUpdate.OnNext(GearUpdateType.Rocked);
        }
        
        public BlockState GetBlockState()
        {
            var stateData = MessagePackSerializer.Serialize(new GearStateData(CurrentRpm.AsPrimitive(), IsCurrentClockwise));
            var state = new BlockState(IGearEnergyTransformer.WorkingStateName, _currentState, stateData);
            return state;
        }
        
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            IsRocked = false;
            var isChanged =
                Math.Abs((CurrentRpm - rpm).AsPrimitive()) > 0.05f ||
                Math.Abs((CurrentTorque - torque).AsPrimitive()) > 0.05f ||
                IsCurrentClockwise != isClockwise;
            
            CurrentRpm = rpm;
            CurrentTorque = torque;
            IsCurrentClockwise = isClockwise;
            
            if (isChanged)
            {
                var state = GetBlockState();
                _onBlockStateChange.OnNext(state);
            }
            
            _currentState = IGearEnergyTransformer.WorkingStateName;
            _onGearUpdate.OnNext(GearUpdateType.SupplyPower);
        }
        
        public void Destroy()
        {
            _onBlockStateChange.Dispose();
        }
    }
    
    public enum GearUpdateType
    {
        SupplyPower,
        Rocked,
    }
}