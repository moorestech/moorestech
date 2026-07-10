using System;
using Core.Master;
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
            // 未選択・使用不能なら待機
            // Stay idle when unselected or unusable
            if (!_context.RecipeGuid.HasValue) return ProcessState.Idle;
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(_context.RecipeGuid.Value);
            if (recipe == null || !_context.InputInventory.IsRecipeUnlocked(recipe) || !_context.InputInventory.IsAllowedToStartProcess(recipe)) return ProcessState.Idle;

            // 抽選を開始時に確定し実スタックで容量確認
            // Fix rolls at start and check capacity with realized stacks
            var effect = _context.EffectComponent.AggregateCurrent();
            var realizedOutputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect);
            var fluidOutputs = MachineOutputFactoryUtil.CreateFluidOutputs(recipe);
            if (!_context.OutputInventory.CanStoreOutputs(realizedOutputs, fluidOutputs))
            {
                return ProcessState.Idle;
            }

            // 産出物と短縮済み時間を確定し、加工ジョブをProcessingStateへ渡して遷移
            // Fix the outputs and the scaled time, hand the job to ProcessingState, then transition
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var totalTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
            var consumedItems = _context.InputInventory.ConsumeInputs(recipe);
            _processingState.SetProcessing(totalTicks, realizedOutputs, fluidOutputs, consumedItems);

            return ProcessState.Processing;
        }
    }
}
