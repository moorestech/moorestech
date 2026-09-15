using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションから拾えたものの一覧。拾えなかったものは Missing に理由付きで残す
    // What could be salvaged from the previous session; whatever could not be is listed in Missing with a reason
    // 作れるのは Clean/Unclean の2つのファクトリだけ。「退避していない」「閉じたゲート」を正常終了の値で偽装させないため（F13）
    // Only the Clean/Unclean factories can build one, so "never salvaged" or "a closed gate" can no longer pose as a clean exit (F13)
    public sealed class PreviousSessionArtifacts
    {
        // 異常終了したセッションが無く、未応答のクラッシュ資料も無いか。確認ゲートを出すかの唯一の判断材料
        // Whether no session exited uncleanly and no crash evidence awaits an answer; the sole basis for showing the confirmation gate
        public bool PreviousExitWasClean { get; }

        // 退避が読み書きした last-session。未応答の印（PendingCrashReportMark）を消す側がここを使う
        // The last-session the salvage worked in; whoever clears the pending mark (PendingCrashReportMark) uses it
        public string LastSessionDirectory { get; }

        // 印を消費した前回プロセスごとの終了状態。同じpidに複数セッションがあれば、1つでも異常なら false
        // The exit state per previous process whose marks were consumed; with several sessions under one pid, any unclean one makes it false
        public IReadOnlyDictionary<int, bool> ExitedCleanlyByProcessId { get; }

        // 退避できた前回プロセスのpid。0件なら退避物は前世代の持ち越しか、そもそも無い
        // Pids whose files were actually salvaged; zero means the artifacts are a carry-over from an older generation, or absent
        public IReadOnlyList<int> SalvagedProcessIds { get; }

        public string RecordingDirectory { get; }
        public string SnapshotsDirectory { get; }
        public string PlayerLogPath { get; }
        public IReadOnlyList<string> CrashDumpFiles { get; }

        // 落ちたセッションが開始時に書き残した出所。読めなければnullで、理由は Missing にある（F12）
        // The origin the crashed session wrote at its start; null when unreadable, with the reason in Missing (F12)
        public SessionOriginSnapshot PreviousOrigin { get; }

        // 箱を閉じられず戻し切れなかった分を書き出し側が追記するため、ここだけ可変のリストのまま持つ
        // Kept as a mutable list because the writer appends what it could not restore after failing to close a box
        public List<MissingItem> Missing { get; }

        public bool HasAnythingToSend => RecordingDirectory != null || SnapshotsDirectory != null || PlayerLogPath != null || 0 < CrashDumpFiles.Count;

        private PreviousSessionArtifacts(bool previousExitWasClean, string lastSessionDirectory, IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId, IReadOnlyList<int> salvagedProcessIds,
            string recordingDirectory, string snapshotsDirectory, string playerLogPath, IReadOnlyList<string> crashDumpFiles, SessionOriginSnapshot previousOrigin, List<MissingItem> missing)
        {
            PreviousExitWasClean = previousExitWasClean;
            LastSessionDirectory = lastSessionDirectory;
            ExitedCleanlyByProcessId = exitedCleanlyByProcessId;
            SalvagedProcessIds = salvagedProcessIds;
            RecordingDirectory = recordingDirectory;
            SnapshotsDirectory = snapshotsDirectory;
            PlayerLogPath = playerLogPath;
            CrashDumpFiles = crashDumpFiles;
            PreviousOrigin = previousOrigin;
            Missing = missing;
        }

        public static PreviousSessionArtifacts Clean(string lastSessionDirectory, IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId, List<MissingItem> missing)
        {
            return new PreviousSessionArtifacts(true, lastSessionDirectory, exitedCleanlyByProcessId, new List<int>(), null, null, null, new List<string>(), null, missing);
        }

        public static PreviousSessionArtifacts Unclean(string lastSessionDirectory, string recordingDirectory, string snapshotsDirectory, string playerLogPath, IReadOnlyList<string> crashDumpFiles,
            IReadOnlyList<int> salvagedProcessIds, IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId, SessionOriginSnapshot previousOrigin, List<MissingItem> missing)
        {
            return new PreviousSessionArtifacts(false, lastSessionDirectory, exitedCleanlyByProcessId, salvagedProcessIds, recordingDirectory, snapshotsDirectory, playerLogPath, crashDumpFiles, previousOrigin, missing);
        }
    }
}
