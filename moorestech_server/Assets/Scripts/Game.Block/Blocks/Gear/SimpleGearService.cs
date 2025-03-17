using System;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearService
    {
        private string _currentState = IGearEnergyTransformer.WorkingStateName;
        public IObservable<Unit> BlockStateChange => _onBlockStateChange;
        private readonly Subject<Unit> _onBlockStateChange = new();
        
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
            
            _onBlockStateChange.OnNext(Unit.Default);
            _onGearUpdate.OnNext(GearUpdateType.Rocked);
        }
        
        public BlockStateDetail GetBlockStateDetail()
        {
            var stateData = MessagePackSerializer.Serialize(new GearStateDetail(IsCurrentClockwise, CurrentRpm.AsPrimitive(), CurrentTorque.AsPrimitive()));
            return new BlockStateDetail(GearStateDetail.BlockStateDetailKey, stateData);
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
                _onBlockStateChange.OnNext(Unit.Default);
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