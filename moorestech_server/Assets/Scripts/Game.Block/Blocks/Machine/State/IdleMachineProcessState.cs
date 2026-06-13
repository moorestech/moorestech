using System;
using Core.Update;
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
            // レシピの有無と開始可否を確認
            // Check the recipe presence and whether processing may start
            var isGetRecipe = _context.InputInventory.TryGetRecipeElement(out var recipe);
            if (!isGetRecipe || !_context.InputInventory.IsAllowedToStartProcess())
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

            // 産出物と短縮済み時間を確定し、加工ジョブをProcessingStateへ渡して遷移
            // Fix the outputs and the scaled time, hand the job to ProcessingState, then transition
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var totalTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
            _processingState.SetProcessing(recipe, realizedOutputs, totalTicks);

            return ProcessState.Processing;
        }
    }
}
