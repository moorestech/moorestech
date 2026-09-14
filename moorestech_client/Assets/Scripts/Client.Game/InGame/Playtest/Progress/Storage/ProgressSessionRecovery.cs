using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 前回セッションの書きかけを起動時に必ず畳む。異常終了なら crash-recovered、正常終了でも残っていれば quit で送る
    // Always folds a half-written previous session at boot: crash-recovered after an unclean exit, quit if it lingered after a clean one
    public static class ProgressSessionRecovery
    {
        // 消費と同じ起動時1箇所から呼ばれる。今回のセッションを書く側（ProgressRecorder）はこれを知らない
        // Called from the single boot-time spot that consumes the marks; the writer of this session (ProgressRecorder) knows nothing of it
        public static List<string> RecoverLeftoverSessions(bool previousExitWasClean)
        {
            var scan = ProgressCurrentSession.ScanLeftovers();
            var bundles = new List<string>();
            foreach (var directory in scan.Directories)
            {
                var bundle = RecoverLeftoverSession(directory, previousExitWasClean, scan.Skipped);
                if (bundle != null) bundles.Add(bundle);
            }
            return bundles;
        }

        // 残骸が無ければ null。回収した場合は書き出した outbox の箱を返す
        // Returns null when there is no leftover; otherwise the outbox box it wrote
        public static string RecoverLeftoverSession(string sessionDirectory, bool previousExitWasClean, IReadOnlyList<MissingItem> skipped)
        {
            if (!ProgressRecordFiles.HasCurrentSession(sessionDirectory)) return null;

            var missing = new List<MissingItem>(skipped);
            var header = ProgressRecordFiles.ReadHeader(sessionDirectory);
            var events = ProgressRecordFiles.ReadEvents(sessionDirectory, out _);
            var sessionEnd = ResolveSessionEnd();
            var endReason = previousExitWasClean ? ProgressEndReason.Quit : ProgressEndReason.CrashRecovered;

            var bundle = ProgressRecordFiles.CloseCurrentInto(sessionDirectory, endReason, sessionEnd, missing);
            if (bundle == null) Debug.LogWarning($"前回の進行記録を閉じられなかったため送れません {sessionDirectory} endReason:{endReason}");
            return bundle;

            #region Internal

            // 終了時刻は分からないので最後のイベント時刻を使う。イベントが無ければ開始時刻に潰れる
            // The exit time is unknown, so the last event's time is used; with no events it collapses to the session start
            DateTime ResolveSessionEnd()
            {
                var last = 0 < events.Count ? events[events.Count - 1].T : header?.SessionStart;
                if (ProgressUtcTime.TryParseIso(last, out var parsed)) return parsed;

                // 読めない時刻で潰すと playSeconds が回収時刻基準になる。壊れていた事実を残す
                // Collapsing an unreadable time bases playSeconds on the recovery moment, so the corruption is recorded
                var reason = $"前回セッションの終了時刻を読めないため回収時刻で代用した value:{last}";
                Debug.LogWarning($"進行記録の回収: {reason}");
                missing.Add(new MissingItem { Item = "sessionEnd", Reason = reason });
                return DateTime.UtcNow;
            }

            #endregion
        }
    }
}
