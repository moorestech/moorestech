using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Overlay;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     待機を実況記録（待機行・完了印）付きで行う操作群。PlaytestDriverの待機系APIの本体
    ///     Waits recorded with a wait line and completion mark; the bodies behind PlaytestDriver's wait APIs
    /// </summary>
    public sealed class PlaytestRecordedWaits
    {
        private readonly PlaytestReporter _reporter;
        private readonly PlaytestOverlayLogEntry _entry;
        private readonly string _label;
        private readonly float _timeoutSeconds;
        private readonly float _deadline;

        private PlaytestRecordedWaits(PlaytestReporter reporter, string label, float timeoutSeconds)
        {
            _reporter = reporter;
            _label = label;
            _timeoutSeconds = timeoutSeconds;
            _entry = reporter.BeginWait(label);
            _deadline = Time.realtimeSinceStartup + timeoutSeconds;
        }

        // 条件評価を外へ残し、表示・成否・期限を扱う
        // Keep condition evaluation with the caller; own the wait display, result and deadline here
        public static PlaytestRecordedWaits Begin(PlaytestReporter reporter, string label, float timeoutSeconds) => new(reporter, label, timeoutSeconds);

        public void Complete()
        {
            _reporter.EndWait(_entry);
            _reporter.RecordUntilResult(true, _label, "condition met");
        }

        public void CheckTimeout()
        {
            if (_deadline < Time.realtimeSinceStartup)
            {
                _reporter.RecordUntilResult(false, _label, $"Until timeout after {_timeoutSeconds}s");
                throw new TimeoutException($"Until '{_label}' timed out after {_timeoutSeconds}s");
            }
        }

        public static async UniTask<BlockGameObject> WaitBlockGameObject(PlaytestReporter reporter, Vector3Int position)
        {
            var waitEntry = reporter.BeginWait($"ブロック生成待ち: {position}");
            var blockGameObject = await PlaytestBlockOps.WaitBlockGameObjectSpawn(position, 15f);
            reporter.EndWait(waitEntry);
            return blockGameObject;
        }

        public static async UniTask UntilWebUiElement(PlaytestReporter reporter, string testid, float timeoutSeconds)
        {
            // DOM要素待ちを待機表示し、成功後も録画用の操作間隔を確保する
            // Show DOM waiting and retain the recording pace after success
            var waitEntry = reporter.BeginWait($"Web UI要素待ち: {testid}");
            await PlaytestWebUiOps.WaitWebUiElement(testid, timeoutSeconds);
            reporter.EndWait(waitEntry);
            await UniTask.Delay(TimeSpan.FromSeconds(PlaytestReporter.ActionIntervalSeconds), ignoreTimeScale: true);
        }

        public static async UniTask WaitUiState(PlaytestReporter reporter, UIStateEnum state, float timeoutSeconds)
        {
            var waitEntry = reporter.BeginWait($"UI状態待ち: {state}");
            await PlaytestUiOps.WaitUiState(state, timeoutSeconds);
            reporter.EndWait(waitEntry);
        }

        public static async UniTask WaitSeconds(PlaytestReporter reporter, float seconds)
        {
            reporter.BeginWait($"{seconds:0.#}秒");
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
        }
    }
}
