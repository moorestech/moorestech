using System;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    public class SimpleGearService
    {
        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;
        private Subject<ChangedBlockState> _onBlockStateChange = new();

        public float CurrentRpm { get; private set; }
        public float CurrentTorque { get; private set; }
        public bool IsCurrentClockwise { get; private set; }
        private string _currentState = IGearEnergyTransformer.WorkingStateName;

        public void Rocked()
        {
            _currentState = IGearEnergyTransformer.RockedStateName;
            CurrentRpm = 0;
            CurrentTorque = 0;

            var state = new ChangedBlockState(IGearEnergyTransformer.RockedStateName, _currentState);
            _onBlockStateChange.OnNext(state);
        }

        public void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            var isChanged =
                Math.Abs(CurrentRpm - rpm) > 0.05f ||
                Math.Abs(CurrentTorque - torque) > 0.05f ||
                IsCurrentClockwise != isClockwise;

            CurrentRpm = rpm;
            CurrentTorque = torque;
            IsCurrentClockwise = isClockwise;

            if (isChanged)
            {
                var stateData = MessagePackSerializer.Serialize(new GearStateData(rpm, isClockwise));
                var state = new ChangedBlockState(IGearEnergyTransformer.WorkingStateName, _currentState, stateData);
                _onBlockStateChange.OnNext(state);
            }
            _currentState = IGearEnergyTransformer.WorkingStateName;
        }

        public void Destroy()
        {
            _onBlockStateChange = null;
        }
    }
}