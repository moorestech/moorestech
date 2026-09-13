using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 前回セッションの書きかけを起動時に必ず畳む。異常終了なら crash-recovered、正常終了でも残っていれば quit で送る
    // Always folds a half-written previous session at boot: crash-recovered after an unclean exit, quit if it lingered after a clean one
    public static class ProgressSessionRecovery
    {
        public const string CrashRecoveredEndReason = "crash-recovered";
        public const string QuitEndReason = "quit";

        // 残骸が無ければ null。回収した場合は書き出した outbox の箱を返す
        // Returns null when there is no leftover; otherwise the outbox box it wrote
        public static string RecoverLeftoverSession(bool previousExitWasClean)
        {
            if (!ProgressRecordFiles.HasCurrentSession()) return null;

            // 終了時刻は分からないので最後のイベント時刻を使う。イベントが無ければ開始時刻に潰れる
            // The exit time is unknown, so the last event's time is used; with no events it collapses to the session start
            var header = ProgressRecordFiles.ReadHeader();
            var sessionEnd = ResolveSessionEnd(header, ProgressRecordFiles.ReadEvents());
            var endReason = previousExitWasClean ? QuitEndReason : CrashRecoveredEndReason;

            var bundle = ProgressRecordFiles.CloseCurrentInto(endReason, sessionEnd);
            if (bundle == null) Debug.LogWarning($"前回の進行記録を閉じられなかったため送れません endReason:{endReason}");
            return bundle;
        }

        private static DateTime ResolveSessionEnd(ProgressRecordHeader header, List<ProgressEventEntry> events)
        {
            var last = events.Count > 0 ? events[events.Count - 1].T : header?.SessionStart;
            const DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
            return DateTime.TryParse(last, null, styles, out var parsed) ? parsed : DateTime.UtcNow;
        }
    }
}
