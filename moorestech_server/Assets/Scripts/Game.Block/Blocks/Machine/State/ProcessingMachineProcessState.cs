using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine.State.Util;
using Game.Block.Blocks.Util;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート。電力に応じて進行し、完了で待機へ戻る
    // Processing state: advances with power and returns to idle on completion
    internal class ProcessingMachineProcessState : IMachineProcessState
    {
        // 進行中レシピ(返却用)。無ければnull
        // Recipe of the running job (for refund calculation); null when no job exists
        public MachineRecipeMasterElement CurrentRecipe { get; private set; }
        
        public ProcessState State => ProcessState.Processing;
        private readonly MachineProcessContext _context;
        public Guid RecipeGuid => CurrentRecipe?.MachineRecipeGuid ?? Guid.Empty;
        
        public uint TotalTicks { get; private set; }
        public uint RemainingTicks  { get; private set; }
        
        public IReadOnlyList<IItemStack> PendingOutputs => _pendingOutputs;
        private List<IItemStack> _pendingOutputs;

        // 完了直前に産出リストを差し替えるフック（清浄室のチップ抽選など、OnExit挿入前の置き換え用）
        // Hook to replace the pending output list just before completion (e.g. clean-room chip draw swaps items before OnExit inserts them)
        public void ReplacePendingOutputs(List<IItemStack> outputs)
        {
            _pendingOutputs = outputs;
        }

        
        // 出力を払い出さずジョブを破棄(返却用)
        // Discard the job without paying outputs (used by the recipe-change refund flow)
        public void CancelProcessing()
        {
            _pendingOutputs = null;
            CurrentRecipe = null;
            TotalTicks = 0;
            RemainingTicks = 0;
        }
        
        public ProcessingMachineProcessState(MachineProcessContext context, uint remainingTicks, MachineRecipeMasterElement recipe, List<IItemStack> pendingOutputs)
        {
            _context = context;
            RemainingTicks = remainingTicks;

            // レシピがあれば加工を復元する。産出予定nullの旧セーブは完了時に再抽選する
            // Restore processing whenever a recipe exists; old saves with null pending outputs re-roll on completion
            if (recipe != null)
            {
                SetProcessing(recipe, pendingOutputs);
            }
        }


        // 加工するジョブをIdle、ロードから設定
        // Set the processing job from Idle or on load
        public void SetProcessing(MachineRecipeMasterElement recipe, List<IItemStack> pendingOutputs)
        {
            CurrentRecipe = recipe;
            _pendingOutputs = pendingOutputs;
            
            var effect = _context.EffectComponent.AggregateCurrent();
            
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var totalTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * effect.ProcessingTimeMultiplier));
            TotalTicks = totalTicks;
        }

        // 開始時に入力を消費し残りtickを設定する
        // Consume inputs and set remaining ticks on start
        public void OnEnter()
        {
            _context.InputInventory.ReduceInputSlot(CurrentRecipe);
            RemainingTicks = TotalTicks;
        }

        public ProcessState GetNextUpdate()
        {
            // 電力、モジュールに基づいてこのティックで引くティック数を計算
            // Calculate the number of ticks to consume this tick based on power and modules
            var effectiveRequestPower = _context.EffectiveRequestPower(ProcessState.Processing);
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_context.CurrentPower, effectiveRequestPower);

            if (0 < RemainingTicks)
            {
                // 残りtickが尽きるまでは加工継続
                // Keep processing until remaining ticks run out
                if (subTicks < RemainingTicks)
                {
                    RemainingTicks -= subTicks;
                    return ProcessState.Processing;
                }
                RemainingTicks = 0;
            }

            // 旧セーブで産出予定が無い場合はここで一度だけ確定させる（以後の保留tickで再抽選しないため）
            // Old saves lacking pending outputs get rolled once here (so held ticks afterward never re-roll)
            _pendingOutputs ??= _context.CreateRealizedOutputs(CurrentRecipe);

            // tickは尽きたが産出物が収まらない間は、加工ではなく出力詰まりとして待つ（要求電力を待機率へ落とす）
            // Once ticks are exhausted but the outputs do not fit, wait as output-blocked rather than processing (drops the request to the idle rate)
            if (!CanStoreRealizedOutputs()) return ProcessState.OutputBlocked;

            return ProcessState.Idle;
        }

        // 確定済み産出物が出力先へ収まるか。出力詰まりステートの復帰判定と共有する
        // Whether the fixed outputs fit; shared with the output-blocked state's resume decision
        public bool CanStoreRealizedOutputs()
        {
            if (CurrentRecipe == null) return true;
            return _context.OutputInventory.CanStoreOutputs(_pendingOutputs, MachineOutputFactoryUtil.CreateFluidOutputs(CurrentRecipe));
        }

        // 完了時に産出物を払い出す。詰まっている間は保持したまま出力詰まりステートへ引き継ぐ
        // Pay the outputs on completion; while blocked the job stays held and is handed to the output-blocked state
        public void OnExit()
        {
            if (!CanStoreRealizedOutputs()) return;
            PayoutAndClear();
        }

        // 産出物を払い出してジョブを空にする（旧セーブは産出予定が無いため再抽選）
        // Pay the outputs and empty the job (re-roll for old saves that lack pending outputs)
        public void PayoutAndClear()
        {
            if (CurrentRecipe == null) return;

            var outputs = _pendingOutputs ?? _context.CreateRealizedOutputs(CurrentRecipe);
            _context.OutputInventory.InsertOutputSlot(outputs, MachineOutputFactoryUtil.CreateFluidOutputs(CurrentRecipe));

            // 加工情報をクリアしてIdleが古いレシピ/進捗を報告・保存しないようにする
            // Clear the processing snapshot so idle does not report or serialize stale recipe/progress
            _pendingOutputs = null;
            CurrentRecipe = null;
            TotalTicks = 0;
            RemainingTicks = 0;
        }
    }
}
