using System;
using System.Globalization;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回異常終了の確認にまだ誰も答えていない印。last-session に永続化し、ゲートを出さない起動を跨いでも資料を消させない（F04）
    // Marks that nobody has answered the previous-crash confirmation yet; persisted in last-session so launches that show no gate never discard the evidence (F04)
    // 置くのは異常終了を検知した退避、消すのはゲートに答えた（送った・送らないを選んだ）ときだけ
    // Placed by a salvage that detected an unclean exit, and removed only when the gate is answered (sent, or declined)
    public static class PendingCrashReportMark
    {
        public const string FileName = "pending_crash_report";

        public static string PathIn(string lastSessionDirectory)
        {
            return Path.Combine(lastSessionDirectory, FileName);
        }

        public static void MarkPending(string lastSessionDirectory)
        {
            // 印の書き込みはディスクIO。権限や他Editorのロックで失敗しても起動は続ける
            // Writing the mark is disk IO; boot continues even if permissions or another Editor's lock make it fail
            try
            {
                Directory.CreateDirectory(lastSessionDirectory);
                File.WriteAllText(PathIn(lastSessionDirectory), DateTime.UtcNow.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture));
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"未応答のクラッシュ報告の印を書けませんでした（ゲートに答えないまま次に正常起動すると前回の資料が消えます） {lastSessionDirectory}: {e.Message}");
            }
        }

        public static bool IsPending(string lastSessionDirectory)
        {
            return File.Exists(PathIn(lastSessionDirectory));
        }

        public static void Clear(string lastSessionDirectory)
        {
            var deletion = SalvageFileOperations.DeleteFile(PathIn(lastSessionDirectory));
            if (!deletion.Succeeded) Debug.LogWarning($"未応答のクラッシュ報告の印を消せませんでした（次回起動でも同じ確認が再提示されます） {lastSessionDirectory}: {deletion.FailureReason}");
        }
    }
}
