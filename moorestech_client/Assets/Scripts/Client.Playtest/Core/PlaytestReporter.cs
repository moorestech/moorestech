using System;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     操作の実況記録係。オーバーレイ表示・result.jsonのTimeline・アクション間インターバルを一手に担う
    ///     Play-by-play recorder: feeds the overlay, the result Timeline, and paces actions with an interval
    /// </summary>
    public class PlaytestReporter
    {
        // 人間が動画で操作を追えるよう1アクションごとに置く間隔
        // Gap inserted after every action so humans can follow the recording
        public const float ActionIntervalSeconds = 0.5f;

        private readonly PlaytestResult _result;

        public PlaytestReporter(PlaytestResult result)
        {
            _result = result;
        }

        public void Step(string label)
        {
            PlaytestOverlay.PushStep(label);
            AppendTimeline("STEP", label);
        }

        public void Note(string message)
        {
            PlaytestOverlay.PushNote(message);
            AppendTimeline("NOTE", message);
            Debug.Log($"[Playtest] note: {message}");
        }

        public async UniTask Act(string label, Func<UniTask> action)
        {
            // ステップ表示→実行→0.5秒の認知用インターバル
            // Show the step, run the action, then insert the 0.5s comprehension gap
            Step(label);
            await action();
            await UniTask.Delay(TimeSpan.FromSeconds(ActionIntervalSeconds), ignoreTimeScale: true);
        }

        public PlaytestOverlayLogEntry BeginWait(string label)
        {
            AppendTimeline("WAIT", label);
            return PlaytestOverlay.PushWait($"待機: {label}");
        }

        public void EndWait(PlaytestOverlayLogEntry entry)
        {
            entry.Text += " ✔";
        }

        public void RecordAssert(bool passed, string label, string message)
        {
            _result.Asserts.Add(new PlaytestAssertResult { Label = label, Passed = passed, Message = message });
            PlaytestOverlay.PushAssert(label, passed);
            AppendTimeline(passed ? "PASS" : "FAIL", label);
        }

        public void RecordUntilResult(bool passed, string label, string message)
        {
            // Untilは待機行が既に出ているため、成功時のオーバーレイ行は追加しない（失敗のみ強調）
            // Until already shows a wait line, so skip the overlay row on success (highlight failures only)
            _result.Asserts.Add(new PlaytestAssertResult { Label = label, Passed = passed, Message = message });
            if (!passed) PlaytestOverlay.PushAssert(label, false);
            AppendTimeline(passed ? "PASS" : "FAIL", label);
        }

        private void AppendTimeline(string tag, string text)
        {
            var elapsed = Time.realtimeSinceStartup - PlaytestOverlay.State.RunStartRealtime;
            _result.Timeline.Add($"{elapsed,7:F1}s [{tag}] {text}");
        }
    }
}
