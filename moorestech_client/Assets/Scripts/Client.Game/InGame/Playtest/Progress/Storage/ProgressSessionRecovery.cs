using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // 前回セッションの書きかけを起動時に必ず畳む。終了の仕方は残骸のpidごとに、印を消費した結果から決める（C36）
    // Always folds half-written previous sessions at boot; how each ended is decided per leftover pid from the consumed marks (C36)
    public static class ProgressSessionRecovery
    {
        private const string EndReasonMissingItem = "endReason";

        // 消費と同じ起動時1箇所から呼ばれる。今回のセッションを書く側（ProgressRecorder）はこれを知らない
        // Called from the single boot-time spot that consumes the marks; the writer of this session (ProgressRecorder) knows nothing of it
        public static List<string> RecoverLeftoverSessions(IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId)
        {
            var scan = ProgressCurrentSession.ScanLeftovers();
            var currentProcessId = RecordingProcessDirectories.CurrentProcessId();
            var bundles = new List<string>();
            foreach (var leftover in scan.Sessions)
            {
                var missing = new List<MissingItem>(scan.Skipped);
                var endReason = ResolveEndReason(leftover.ProcessId, currentProcessId, exitedCleanlyByProcessId, missing);
                var closeResult = ProgressRecordFiles.CloseLeftoverInto(leftover.Path, endReason, missing);

                if (closeResult.BundleDirectory != null) bundles.Add(closeResult.BundleDirectory);
                else if (closeResult.WriteFailed) Debug.LogWarning($"前回の進行記録を閉じられなかったため送れません（次回起動で回収し直します） {leftover.Path}");
                else Debug.Log($"前回の進行記録の段に畳む中身がありませんでした {leftover.Path}");
            }
            return bundles;
        }

        // 印を消費したpidだけが終了の仕方を知っている。分からない残骸は crash-recovered とし、理由を欠損列に名乗る
        // Only a pid whose marks were consumed knows how it ended; an unknown leftover becomes crash-recovered with its reason declared in missing
        internal static ProgressEndReason ResolveEndReason(int processId, int currentProcessId, IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId, List<MissingItem> missing)
        {
            // 自pidの残骸は同じプロセスの前のセッション（Editorの再生し直し）。pid単位の判定では今回のセッションと区別できない
            // A leftover under this pid is an earlier session of this very process (an Editor replay), which a per-pid verdict cannot tell apart from this one
            if (processId == currentProcessId) return DeclareCrashRecovered($"pid {processId} は自プロセスのため、pid単位の終了判定では前のセッションの終わり方を決められない");
            if (!exitedCleanlyByProcessId.TryGetValue(processId, out var exitedCleanly)) return DeclareCrashRecovered($"pid {processId} の終了の印を消費していない（印が無い）ため終わり方が分からない");
            return exitedCleanly ? ProgressEndReason.Quit : ProgressEndReason.CrashRecovered;

            #region Internal

            ProgressEndReason DeclareCrashRecovered(string reason)
            {
                Debug.LogWarning($"進行記録の回収: {reason}（crash-recovered として送ります）");
                missing.Add(new MissingItem { Item = EndReasonMissingItem, Reason = reason });
                return ProgressEndReason.CrashRecovered;
            }

            #endregion
        }
    }
}
