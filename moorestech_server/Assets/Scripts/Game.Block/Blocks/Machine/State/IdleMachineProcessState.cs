using Game.Block.Blocks.Machine.State.Util;

namespace Game.Block.Blocks.Machine.State
{
    // 待機ステート。レシピが揃えば加工ジョブを確定し加工へ遷移する
    // Idle state: fixes the processing job and transitions to processing once a recipe is ready
    internal class IdleMachineProcessState : IMachineProcessState
    {
        private readonly MachineProcessContext _context;
        private readonly ProcessingMachineProcessState _processingState;

        public IdleMachineProcessState(MachineProcessContext context, ProcessingMachineProcessState processingState)
        {
            _context = context;
            _processingState = processingState;
        }

        public ProcessState State => ProcessState.Idle;
        public void OnEnter() { }
        public void OnExit() { }

        public ProcessState GetNextUpdate()
        {
            // 選択レシピが無ければ加工しない（レシピ選択必須）
            // Never process without a selected recipe (selection is mandatory)
            var recipe = _context.SelectedRecipe;
            if (recipe == null || !_context.InputInventory.IsAllowedToStartProcess(recipe))
            {
                return ProcessState.Idle;
            }

            // 抽選を開始時に確定し実スタックで容量確認
            // Fix rolls at start and check capacity with realized stacks
            var effect = _context.EffectComponent.AggregateCurrent();
            var realizedOutputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect);
            if (!_context.OutputInventory.CanStoreOutputs(realizedOutputs, MachineOutputFactoryUtil.CreateFluidOutputs(recipe)))
            {
                return ProcessState.Idle;
            }

            // ProcessingStateへ遷移
            // Hand the job to ProcessingState and transition
            _processingState.SetProcessing(recipe, realizedOutputs);
            return ProcessState.Processing;
        }
    }
}
