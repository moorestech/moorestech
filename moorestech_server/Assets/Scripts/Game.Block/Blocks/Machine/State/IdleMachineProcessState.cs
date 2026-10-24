using Game.Block.Blocks.Machine.State.Util;

namespace Game.Block.Blocks.Machine.State
{
    // 待機ステート。レシピが揃えば加工ジョブを確定し加工へ遷移する
    // Idle state: fixes the processing job and transitions to processing once a recipe is ready
    internal class IdleMachineProcessState : IMachineProcessState, IMachinePendingRealizationReader
    {
        private readonly MachineProcessContext _context;
        private readonly ProcessingMachineProcessState _processingState;

        // 出力先が空くのを待つ間、確定済みの出力と加工時間を保持する（レシピか継承レベルが変わるまで確定し直さない）
        // Holds the realized outputs and processing time while waiting for output space (never re-realized until the recipe or inherited level changes)
        private MachineRealization _pendingRealization;

        public IdleMachineProcessState(MachineProcessContext context, ProcessingMachineProcessState processingState)
        {
            _context = context;
            _processingState = processingState;
        }

        public ProcessState State => ProcessState.Idle;
        public void OnEnter() { }
        public void OnExit() { }

        public bool TryGetPendingRealization(out MachineRealization realization)
        {
            realization = _pendingRealization;
            return realization != null;
        }

        public ProcessState GetNextUpdate()
        {
            // 選択レシピが無ければ加工しない（レシピ選択必須）
            // Never process without a selected recipe (selection is mandatory)
            var recipe = _context.SelectedRecipe;
            if (recipe == null || !_context.InputInventory.TryGetReadyInput(recipe, out var inheritedLevel))
            {
                _pendingRealization = null;
                return ProcessState.Idle;
            }

            // レシピか継承レベルが変わったときだけ新規に確定する。同じ組で待機している間は既存の確定結果を使い回す
            // Realize anew only when the recipe or inherited level changed; while waiting on the same pair, reuse the result
            if (_pendingRealization == null || _pendingRealization.Recipe != recipe || _pendingRealization.InheritedLevel != inheritedLevel)
            {
                _pendingRealization = _context.CreateRealization(recipe, inheritedLevel);
            }

            if (!_context.OutputInventory.CanStoreOutputs(_pendingRealization.Outputs, MachineOutputFactoryUtil.CreateFluidOutputs(recipe)))
            {
                return ProcessState.Idle;
            }

            // 確定時の加工時間ごと ProcessingState へ渡して遷移する
            // Hand the job with its realized processing time to ProcessingState and transition
            _processingState.SetProcessing(recipe, _pendingRealization.Outputs, inheritedLevel, _pendingRealization.TotalTicks);
            _pendingRealization = null;
            return ProcessState.Processing;
        }
    }
}
