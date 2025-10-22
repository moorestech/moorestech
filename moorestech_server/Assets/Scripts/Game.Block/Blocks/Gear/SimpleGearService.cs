using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearService
    {
        public RPM CurrentRpm { get; private set; }
        public Torque CurrentTorque { get; private set; }
        public bool IsCurrentClockwise { get; private set; }
        
        public IObservable<Unit> BlockStateChange => _onBlockStateChange;
        private readonly Subject<Unit> _onBlockStateChange = new();
        
        public IObservable<GearUpdateType> OnGearUpdate => _onGearUpdate;
        private readonly Subject<GearUpdateType> _onGearUpdate = new();
        private readonly BlockInstanceId _blockInstanceId;
        
        private string _currentState = IGearEnergyTransformer.WorkingStateName;
        
        public SimpleGearService(BlockInstanceId blockInstanceId)
        {
            _blockInstanceId = blockInstanceId;
        }
        
        public void StopNetwork()
        {
            _currentState = IGearEnergyTransformer.RockedStateName;
            CurrentRpm = new RPM(0);
            CurrentTorque = new Torque(0);

            _onBlockStateChange.OnNext(Unit.Default);
            _onGearUpdate.OnNext(GearUpdateType.Rocked);
        }
        
        public BlockStateDetail GetBlockStateDetail()
        {
            var network = GearNetworkDatastore.GetGearNetwork(_blockInstanceId);
            var info = network.CurrentGearNetworkInfo;
            var stateDetail = new GearStateDetail(IsCurrentClockwise, CurrentRpm.AsPrimitive(), CurrentTorque.AsPrimitive(), info);

            return new BlockStateDetail(GearStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }
        
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
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