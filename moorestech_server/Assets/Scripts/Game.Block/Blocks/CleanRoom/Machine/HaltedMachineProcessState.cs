using System;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.State;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 清浄室条件が満たされるまで加工状態を凍結する
    // Freezes machine processing until clean-room conditions are satisfied
    internal class HaltedMachineProcessState : IMachineProcessState
    {
        private readonly ProcessingMachineProcessState _processingState;
        private readonly Func<bool> _canOperate;

        public HaltedMachineProcessState(ProcessingMachineProcessState processingState, Func<bool> canOperate)
        {
            _processingState = processingState;
            _canOperate = canOperate;
        }

        public ProcessState State => ProcessState.Halted;
        public void OnEnter() { }
        public void OnExit() { }

        public ProcessState GetNextUpdate()
        {
            if (!_canOperate()) return ProcessState.Halted;
            return _processingState.HasProcessing ? ProcessState.Processing : ProcessState.Idle;
        }
    }
}
