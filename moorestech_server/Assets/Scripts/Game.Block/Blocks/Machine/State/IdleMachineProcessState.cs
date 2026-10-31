using System;
using Game.Block.Blocks.Machine.State.Util;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

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

        // 確定出力の空き待ち（直近判定）
        // Whether the latest check found realized outputs waiting for space
        public bool IsWaitingForOutputSpace { get; private set; }

        // tick内の判定でIdleに留まったまま空き待ちが変わった時だけ通知する。tick外の判定し直しは呼び出し側の明示通知に任せる
        // Notifies only when the in-tick check stays Idle and the wait changed; off-tick re-checks leave notifying to the caller's explicit publish
        public IObservable<Unit> OnOutputSpaceWaitChanged => _outputSpaceWaitChanged;
        private readonly Subject<Unit> _outputSpaceWaitChanged = new();

        internal void RestorePending(MachineRealization realization)
        {
            _pendingRealization = realization;
            // 復元直後の初期スナップショットが次tickを待たずに理由を出せるよう、確定済み結果で判定し直す
            // Re-check with the restored result so the first snapshot after loading carries the reason without waiting a tick
            RefreshOutputSpaceWait();
        }

        public ProcessState State => ProcessState.Idle;
        public void OnEnter() => RefreshOutputSpaceWait();
        public void OnExit() { }

        // tick外で状態が書き換わった後（入場・復元・レシピ変更）に、確定済み結果のまま出力空きだけを判定し直す。抽選・確定はしない
        // After an off-tick state rewrite (enter, restore, recipe change), re-check output space against the held result only; never draws or realizes
        internal void RefreshOutputSpaceWait()
        {
            // 保持中の結果が選択レシピのものに限って入力を見る（tick外で入力判定の呼び出しを増やさない）
            // Look at inputs only when the held result is for the selected recipe (no extra off-tick input checks)
            var recipe = _context.SelectedRecipe;
            IsWaitingForOutputSpace = _pendingRealization != null && _pendingRealization.Recipe == recipe
                && _context.InputInventory.TryGetReadyInput(recipe, out var inheritedLevel) && IsPendingStuckFor(recipe, inheritedLevel);
        }

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
            // 入力が欠けても確定済みの抽選は捨てない（出し入れで引き直せないように）。レシピと継承レベルの組が変われば下で確定し直す
            // Keep the fixed draw when inputs go missing (so taking items out and back cannot re-roll); a changed recipe/level pair is realized anew below
            if (recipe == null || !_context.InputInventory.TryGetReadyInput(recipe, out var inheritedLevel))
            {
                StayIdleWithOutputSpaceWait(false);
                return ProcessState.Idle;
            }

            // レシピか継承レベルが変わったときだけ新規に確定する。同じ組で待機している間は既存の確定結果を使い回す
            // Realize anew only when the recipe or inherited level changed; while waiting on the same pair, reuse the result
            if (_pendingRealization == null || !_pendingRealization.IsRealizedFor(recipe, inheritedLevel))
            {
                _pendingRealization = _context.CreateRealization(recipe, inheritedLevel);
            }

            // 入りきらなければ確定結果を保持したまま待ち、その理由を表示へ出す
            // When it does not fit, keep the realized result and publish the wait reason
            if (IsPendingStuckFor(recipe, inheritedLevel))
            {
                StayIdleWithOutputSpaceWait(true);
                return ProcessState.Idle;
            }

            // 確定時の加工時間ごと ProcessingState へ渡して遷移する。理由の解消は遷移の状態通知が運ぶ
            // Hand the job with its realized processing time to ProcessingState and transition; the transition's state notice carries the cleared reason
            IsWaitingForOutputSpace = false;
            _processingState.SetProcessing(recipe, _pendingRealization.Outputs, inheritedLevel, _pendingRealization.TotalTicks, _pendingRealization.PowerMultiplier);
            _pendingRealization = null;
            return ProcessState.Processing;

            #region Internal

            // Idle維持が確定した分岐でだけ呼ぶ。状態詳細がこのtickで安定した時点の変化を通知する
            // Call only on branches that stay Idle, so the change is notified once the detail is stable for this tick
            void StayIdleWithOutputSpaceWait(bool isWaiting)
            {
                if (IsWaitingForOutputSpace == isWaiting) return;
                IsWaitingForOutputSpace = isWaiting;
                _outputSpaceWaitChanged.OnNext(Unit.Default);
            }

            #endregion
        }

        // 出力空き待ちの判定の正本。保持中の結果が揃った入力の組のもので、かつ出力先に入りきらないか（tick内と tick外の両方がここを通る）
        // The single source of the output-space wait: the held result matches the ready input pair and does not fit the output (both tick and off-tick paths use it)
        private bool IsPendingStuckFor(MachineRecipeMasterElement recipe, int? inheritedLevel)
        {
            return _pendingRealization.IsRealizedFor(recipe, inheritedLevel)
                && !_context.OutputInventory.CanStoreOutputs(_pendingRealization.Outputs, MachineOutputFactoryUtil.CreateFluidOutputs(recipe));
        }
    }
}
