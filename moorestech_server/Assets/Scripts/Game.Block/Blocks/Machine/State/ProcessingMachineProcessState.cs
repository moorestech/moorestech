using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.State.Util;
using Game.Block.Blocks.Util;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート。電力に応じて進行し、完了で待機へ戻る
    // Processing state: advances with power and returns to idle on completion
    internal class ProcessingMachineProcessState : IMachineProcessState
    {
        private readonly MachineProcessContext _context;

        // 加工ジョブはこのステートが所有する（Idleが確定→Processingが消費）
        // This state owns the processing job (Idle fixes it, Processing consumes it)
        private MachineRecipeMasterElement _recipe;
        private List<IItemStack> _pendingOutputs;
        private uint _totalTicks;

        public ProcessingMachineProcessState(MachineProcessContext context)
        {
            _context = context;
        }

        public ProcessState State => ProcessState.Processing;

        public Guid RecipeGuid => _recipe?.MachineRecipeGuid ?? Guid.Empty;
        public uint TotalTicks => _totalTicks;
        public IReadOnlyList<IItemStack> PendingOutputs => _pendingOutputs;

        // 加工ジョブを確定する。Idleの開始判定とセーブ復元の双方から呼ばれる
        // Fix the processing job; called both from Idle's start decision and save restoration
        public void SetProcessing(MachineRecipeMasterElement recipe, List<IItemStack> pendingOutputs, uint totalTicks)
        {
            _recipe = recipe;
            _pendingOutputs = pendingOutputs;
            _totalTicks = totalTicks;
        }

        // 開始時に入力を消費し残りtickを設定する
        // Consume inputs and set remaining ticks on start
        public void OnEnter()
        {
            _context.InputInventory.ReduceInputSlot(_recipe);
            _context.RemainingTicks = _totalTicks;
        }

        public ProcessState GetNextUpdate()
        {
            // 電力、モジュールに基づいてこのティックで引くティック数を計算
            // Calculate the number of ticks to consume this tick based on power and modules
            var effectiveRequestPower = _context.RequestPower * _context.EffectComponent.AggregateCurrent().PowerMultiplier;
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_context.CurrentPower, effectiveRequestPower);

            // 電力を消費する
            // Consume power
            _context.UsedPower = true;

            // 残りtickを使い切ったら完了して待機へ
            // Once remaining ticks are exhausted, finish and return to idle
            if (subTicks >= _context.RemainingTicks)
            {
                _context.RemainingTicks = 0;
                return ProcessState.Idle;
            }

            _context.RemainingTicks -= subTicks;
            return ProcessState.Processing;
        }

        // 完了時に産出物を払い出す（旧セーブは産出予定が無いため再抽選）
        // Output the produced items on completion (re-roll for old saves that lack pending outputs)
        public void OnExit()
        {
            var outputs = _pendingOutputs ?? MachineOutputFactoryUtil.CreateRealizedOutputs(_recipe, _context.EffectComponent.AggregateCurrent());
            _context.OutputInventory.InsertOutputSlot(outputs, MachineOutputFactoryUtil.CreateFluidOutputs(_recipe));
            _pendingOutputs = null;
        }
    }
}
