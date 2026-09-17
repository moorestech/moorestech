using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.DiskOperations;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // 「このセッションは outbox のこの箱へ渡した」印（handed_to_<箱名>）。READY と current/ の削除はアトミックでないため、間で落ちた回収が同じ記録を二重に送らない（F18）
    // The "this session was handed to that outbox box" mark (handed_to_<box name>); READY and clearing current/ are not atomic, so a crash in between never makes recovery ship the record twice (F18)
    internal static class ProgressHandOffMarker
    {
        private const string FileNamePrefix = "handed_to_";

        // 箱を確定（READY）する前に置く。印の無い READY 箱は存在しない順序にする
        // Placed before the box is finalized with READY, so a READY box without a mark can never exist
        public static SalvageOperationResult Write(string sessionDirectory, string bundleDirectory)
        {
            return BugReportFileOperations.WriteText(Path.Combine(sessionDirectory, FileNamePrefix + Path.GetFileName(bundleDirectory)), "");
        }

        // 印の指す箱が READY なら渡し済み。READY の無い印は渡し損ねなので、理由を出して通常の回収へ回す
        // A mark whose box has READY means the hand-off completed; one without READY is a failed hand-off, logged and left to the normal recovery
        public static bool TryFindShippedBundle(string sessionDirectory, out string bundleDirectory)
        {
            bundleDirectory = null;
            var listing = BugReportFileOperations.ListFiles(sessionDirectory, FileNamePrefix + "*", out var markers);
            if (!listing.Succeeded)
            {
                Debug.LogWarning($"進行記録の受け渡し印を読めないため、渡し済みかを確かめずに回収します（二重に送る恐れがあります） {sessionDirectory}: {listing.FailureReason}");
                return false;
            }

            foreach (var marker in markers)
            {
                var candidate = Path.Combine(GameSystemPaths.ProgressRecordOutboxDirectory, Path.GetFileName(marker).Substring(FileNamePrefix.Length));
                if (File.Exists(Path.Combine(candidate, BugReportOutbox.ReadyMarkerFileName)))
                {
                    bundleDirectory = candidate;
                    return true;
                }
                Debug.LogWarning($"受け渡し印の指す箱にREADYが無いため、渡し損ねとして回収し直します marker:{marker}");
            }
            return false;
        }
    }
}
