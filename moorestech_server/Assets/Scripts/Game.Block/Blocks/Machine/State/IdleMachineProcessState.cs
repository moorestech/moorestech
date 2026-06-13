using System;
using Core.Update;

namespace Game.Block.Blocks.Machine.State
{
    // 待機ステート。レシピが揃えば開始情報を確定し加工へ遷移する
    // Idle state: fixes the start info and transitions to processing once a recipe is ready
    internal class IdleMachineProcessState : IMachineProcessState
    {
        private readonly MachineProcessContext _context;

        public IdleMachineProcessState(MachineProcessContext context)
        {
            _context = context;
        }

        public ProcessState State => ProcessState.Idle;
        public void OnEnter() { }
        public void OnExit() { }

        public ProcessState GetNextUpdate()
        {
            // レシピの有無と開始可否を確認
            // Check the recipe presence and whether processing may start
            var isGetRecipe = _context.InputInventory.TryGetRecipeElement(out var recipe);
            if (!isGetRecipe || !_context.InputInventory.IsAllowedToStartProcess()) return ProcessState.Idle;

            // 抽選を開始時に確定し実スタックで容量確認
            // Fix rolls at start and check capacity with realized stacks
            var effect = _context.EffectComponent.AggregateCurrent();
            var realizedOutputs = _context.CreateRealizedOutputs(recipe, effect);
            if (!_context.OutputInventory.CanStoreOutputs(realizedOutputs, _context.CreateFluidOutputs(recipe))) return ProcessState.Idle;

            // 産出物と短縮済み時間を確定して加工へ遷移
            // Fix the outputs and the scaled time, then transition to processing
            _context.ProcessingRecipe = recipe;
            _context.PendingOutputs = realizedOutputs;
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            _context.ProcessingRecipeTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
            return ProcessState.Processing;
        }
    }
}
