using System;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.State;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 清浄室条件が満たされるまで加工状態を凍結する。可否は processor が部屋の効果から決めて押し込む
    // Freezes machine processing until clean-room conditions are satisfied; the processor decides operability from the room effect and pushes it
    internal class HaltedMachineProcessState : IMachineProcessState
    {
        private readonly ProcessingMachineProcessState _processingState;
        private bool _canOperate;

        public HaltedMachineProcessState(ProcessingMachineProcessState processingState, bool canOperate)
        {
            _processingState = processingState;
            _canOperate = canOperate;
        }

        public ProcessState State => ProcessState.Halted;
        public void OnEnter() { }
        public void OnExit() { }

        // 部屋の効果が押し込まれるたびに processor から届く
        // Delivered by the processor each time a room effect is pushed
        public void SetCanOperate(bool canOperate)
        {
            _canOperate = canOperate;
        }

        public ProcessState GetNextUpdate()
        {
            if (!_canOperate) return ProcessState.Halted;
            return _processingState.RecipeGuid != Guid.Empty ? ProcessState.Processing : ProcessState.Idle;
        }
    }
}
