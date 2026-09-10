namespace Game.Block.Blocks.Machine.State
{
    // 出力詰まりステート。確定済み産出物を保持したまま出力先が空くのを待つ
    // Output-blocked state: holds the fixed outputs and waits for output space
    internal class OutputBlockedMachineProcessState : IMachineProcessState
    {
        private readonly ProcessingMachineProcessState _processingState;

        public OutputBlockedMachineProcessState(ProcessingMachineProcessState processingState)
        {
            _processingState = processingState;
        }

        public ProcessState State => ProcessState.OutputBlocked;
        public void OnEnter() { }

        public ProcessState GetNextUpdate()
        {
            // レシピを失った復元データは保留ジョブが無いため待機へ戻す
            // A restore that lost its recipe holds no job, so fall back to idle
            if (_processingState.CurrentRecipe == null) return ProcessState.Idle;

            return _processingState.CanStoreRealizedOutputs() ? ProcessState.Idle : ProcessState.OutputBlocked;
        }

        // 出力先が空いた瞬間に、保留していた産出物を払い出して待機へ抜ける
        // The moment space opens, pay the held outputs and leave for idle
        public void OnExit()
        {
            _processingState.PayoutAndClear();
        }
    }
}
