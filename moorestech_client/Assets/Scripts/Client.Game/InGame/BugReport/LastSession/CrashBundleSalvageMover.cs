using System.IO;
using Client.Game.InGame.BugReport.DiskOperations;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 閉じられなかった箱から退避物を戻す。書き出し本体（CrashBundleWriter）から行数分割のために切り出した
    // Gives the salvage back from a box that could not be closed; split out of the writer itself (CrashBundleWriter) to respect the line limit
    internal static class CrashBundleSalvageMover
    {
        // 箱を閉じられなかったときだけ、移した退避物を last-session へ戻す。裁定1の「送り直せる」を実際に成立させる唯一の経路
        // Only when the box could not be closed does the moved salvage go back to last-session; this is what actually makes adjudication 1's "you can resend" true
        internal static void RestoreSalvageFromUnfinishedBundle(string bundleDirectory, PreviousSessionArtifacts artifacts)
        {
            var fullyRestored = RestoreTree(BugReportBundleLayout.RecordingDirectoryName, artifacts.RecordingDirectory);
            fullyRestored &= RestoreTree(BugReportBundleLayout.SnapshotDirectoryName, artifacts.SnapshotsDirectory);

            // 全件戻せたときだけ未完成の箱を消す。残っている箱そのものが「戻し切れなかった証跡がここにある」という印になる
            // The unfinished box is deleted only when everything came back; a box left behind is itself the mark that evidence stayed in it
            if (fullyRestored) DeleteUnfinishedBundle();

            #region Internal

            bool RestoreTree(string item, string salvageDirectory)
            {
                var boxSubDirectory = Path.Combine(bundleDirectory, item);
                if (salvageDirectory == null || !Directory.Exists(boxSubDirectory)) return true;

                var restore = BugReportDiskOperations.MoveTree(boxSubDirectory, salvageDirectory, out var restored);
                if (restore.Succeeded)
                {
                    Debug.LogWarning($"箱を閉じられなかったため退避物を戻しました（次の送信で送り直せます） {salvageDirectory} files:{restored.Count}");
                    return true;
                }

                // 戻しは途中まで進む。開発者ログだけでは次の再送に届かないため、欠損として artifacts へ積み manifest に必ず載せる
                // A restore stops midway, and the developer log never reaches the next resend, so the gap is pushed into artifacts and always lands in the manifest
                var reason = $"未完成の箱 {boxSubDirectory} へ退避物が残ったまま戻せなかった: {restore.FailureReason}";
                Debug.LogError($"退避物を last-session へ戻せませんでした: {reason}");
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = reason });
                return false;
            }

            void DeleteUnfinishedBundle()
            {
                var deletion = BugReportDiskOperations.DeleteDirectory(bundleDirectory);
                if (!deletion.Succeeded) Debug.LogWarning($"閉じられなかった箱を消せませんでした（outbox に残りますがREADYが無いため運搬はされません） {bundleDirectory}: {deletion.FailureReason}");
            }

            #endregion
        }
    }
}
