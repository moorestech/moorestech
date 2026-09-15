using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 退避物と箱の間の移動。書き出し本体（CrashBundleWriter）から行数分割のために切り出した
    // Moves between the salvage and the box; split out of the writer itself (CrashBundleWriter) to respect the line limit
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
                // 退避物を last-session へ戻す move はディスクIO。他プロセスのロックで失敗しても未完成の箱を残すだけで済ませる
                // Moving the salvage back to last-session is disk IO; a failure from another process's lock is tolerated by leaving it in the unfinished box
                try
                {
                    var restored = MoveTree(boxSubDirectory, salvageDirectory);
                    Debug.LogWarning($"箱を閉じられなかったため退避物を戻しました（次の送信で送り直せます） {salvageDirectory} files:{restored.Count}");
                    return true;
                }
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    // 戻しは途中まで進む。開発者ログだけでは次の再送に届かないため、欠損として artifacts へ積み manifest に必ず載せる
                    // A restore stops midway, and the developer log never reaches the next resend, so the gap is pushed into artifacts and always lands in the manifest
                    var reason = $"未完成の箱 {boxSubDirectory} へ退避物が残ったまま戻せなかった: {e.Message}";
                    Debug.LogError($"退避物を last-session へ戻せませんでした: {reason}");
                    artifacts.Missing.Add(new MissingItem { Item = item, Reason = reason });
                    return false;
                }
            }

            void DeleteUnfinishedBundle()
            {
                var deletion = SalvageFileOperations.DeleteDirectory(bundleDirectory);
                if (!deletion.Succeeded) Debug.LogWarning($"閉じられなかった箱を消せませんでした（outbox に残りますがREADYが無いため運搬はされません） {bundleDirectory}: {deletion.FailureReason}");
            }

            #endregion
        }

        // 退避先から箱へは移動で渡す。退避の時点で既に last-session へ改名済みなので、写すと同じ数十MBを2度書くだけになる
        // The salvage moves into the box: it was already renamed into last-session, so copying would write the same tens of megabytes twice
        // コピーへ戻せば再送は成立するが二重書き込みが復活するため、失敗時だけ戻す形で「送り直せる」を満たす
        // Reverting to a copy would also make the resend work, but it brings back the double write, so restoring only on failure buys the same guarantee
        // 退避は pid_<PID>/session_<utcTicks>/ 等の入れ子を保ったまま移すため、こちらも入れ子ごと辿る。戻り値は移した相対パス
        // The salvage keeps nesting such as pid_<PID>/session_<utcTicks>/, so this walks the whole tree too; the relative paths moved are returned
        internal static IReadOnlyList<string> MoveTree(string source, string destination)
        {
            var moved = new List<string>();
            if (source == null || !Directory.Exists(source)) return moved;

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(source, file);
                var destinationFile = Path.Combine(destination, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Move(file, destinationFile);
                moved.Add(relativePath);
            }
            foreach (var subDirectory in Directory.GetDirectories(source)) Directory.Delete(subDirectory, true);
            return moved;
        }
    }
}
