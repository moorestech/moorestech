using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.State.Util;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    // 待機ステート。レシピが揃えば加工ジョブを確定し加工へ遷移する
    // Idle state: fixes the processing job and transitions to processing once a recipe is ready
    internal class IdleMachineProcessState : IMachineProcessState
    {
        private readonly MachineProcessContext _context;
        private readonly ProcessingMachineProcessState _processingState;

        // 出力先が空くのを待つ間、確定済みの実現出力を保持する（レシピが変わるまで再抽選しない）
        // Holds the already-realized outputs while waiting for output space (never re-rolled until the recipe changes)
        private MachineRecipeMasterElement _pendingRecipe;
        private List<IItemStack> _pendingOutputs;

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
                ClearPendingOutputs();
                return ProcessState.Idle;
            }

            // レシピが変わったときだけ新規に抽選する。同じレシピで待機している間は既存の実現結果を使い回す
            // Roll only when the recipe changed; while waiting on the same recipe, reuse the already-realized result
            if (_pendingRecipe != recipe)
            {
                var effect = _context.EffectComponent.AggregateCurrent();
                _pendingOutputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect);
                _pendingRecipe = recipe;
            }

            if (!_context.OutputInventory.CanStoreOutputs(_pendingOutputs, MachineOutputFactoryUtil.CreateFluidOutputs(recipe)))
            {
                return ProcessState.Idle;
            }

            // ProcessingStateへ遷移
            // Hand the job to ProcessingState and transition
            _processingState.SetProcessing(recipe, _pendingOutputs);
            ClearPendingOutputs();
            return ProcessState.Processing;

            #region Internal

            void ClearPendingOutputs()
            {
                _pendingRecipe = null;
                _pendingOutputs = null;
            }

            #endregion
        }
    }
}
