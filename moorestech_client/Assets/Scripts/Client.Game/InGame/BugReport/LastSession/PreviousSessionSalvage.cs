using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 起動直後に前回の記録を退避する。内蔵サーバーのリングと録画リングが上書きを始める前に走らせること
    // Salvages the previous records right at boot, before the embedded server's ring and the recording ring start overwriting
    public static class PreviousSessionSalvage
    {
        public static PreviousSessionArtifacts Artifacts { get; private set; }

        public static PreviousSessionArtifacts RunAtStartup(string worldSnapshotDirectory)
        {
            var wasClean = CleanExitMarker.ConsumePreviousExitCleanFlag();
            Artifacts = Salvage(wasClean, GameSystemPaths.BugReportRecordingDirectory, worldSnapshotDirectory, GameSystemPaths.BugReportLastSessionDirectory);
            Debug.Log($"前回セッションの退避が終わりました clean:{Artifacts.PreviousExitWasClean} sendable:{Artifacts.HasAnythingToSend} missing:{Artifacts.Missing.Count}");
            return Artifacts;
        }

        public static PreviousSessionArtifacts Salvage(bool previousExitWasClean, string recordingDirectory, string worldSnapshotDirectory, string lastSessionDirectory)
        {
            var artifacts = new PreviousSessionArtifacts { PreviousExitWasClean = previousExitWasClean };
            Directory.CreateDirectory(lastSessionDirectory);

            // 正常終了なら前回分は要らない。録画だけ捨て、スナップショットはサーバーのリングに任せる
            // A clean exit needs nothing kept: drop the recording and leave the snapshots to the server's ring
            if (previousExitWasClean)
            {
                ClearDirectory(recordingDirectory);
                return artifacts;
            }

            artifacts.RecordingDirectory = MoveFilesInto(recordingDirectory, Path.Combine(lastSessionDirectory, "recording"), "recording", artifacts);
            artifacts.SnapshotsDirectory = MoveFilesInto(worldSnapshotDirectory, Path.Combine(lastSessionDirectory, "snapshots"), "snapshots", artifacts);

            artifacts.PlayerLogPath = PlayerLogLocator.PreviousSessionLogPath();
            if (artifacts.PlayerLogPath == null) artifacts.Missing.Add(new MissingItem { Item = "playerLog", Reason = "前回セッションのPlayer-prev.logが見つからない" });

            var crashDumpScan = CrashDumpLocator.FindDumpFiles();
            artifacts.CrashDumpFiles = crashDumpScan.Files;
            if (artifacts.CrashDumpFiles.Count == 0) artifacts.Missing.Add(new MissingItem { Item = "crashDump", Reason = CrashDumpMissingReason(crashDumpScan) });

            return artifacts;
        }

        // 「そもそも無かった」と「他アプリとして除外した結果0件」を読み分けられる理由文にする。無音の縮退を残さない
        // The reason distinguishes "there were none" from "all were filtered out as other apps'", leaving no silent degradation
        private static string CrashDumpMissingReason(CrashDumpScanResult scan)
        {
            var roots = string.Join(", ", CrashDumpLocator.CandidateRoots());
            if (scan.ExcludedAsOtherApps == 0) return $"クラッシュダンプが見つからない（探索先: {roots}）";
            return $"共有置き場に{scan.ExcludedAsOtherApps}件あったが自プロセス（{Application.productName} / {CrashDumpLocator.EditorProcessName}）のものは0件だった（除外元: {string.Join(", ", scan.ExcludedRoots)}、探索先: {roots}）";
        }

        // 中身のあるディレクトリだけを退避先へ移す。GameFrameRecorderはpid_<PID>のサブディレクトリへ書くため再帰的に走査する
        // Moves only a non-empty directory; recurses because GameFrameRecorder writes under a pid_<PID> subdirectory
        private static string MoveFilesInto(string source, string destination, string item, PreviousSessionArtifacts artifacts)
        {
            if (source == null || !Directory.Exists(source))
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が無い: {source}" });
                return null;
            }
            var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が空: {source}" });
                return null;
            }

            ClearDirectory(destination);
            Directory.CreateDirectory(destination);
            // サブディレクトリ構造(pid_<PID>/segment-0.mp4等)を保ったまま退避先へ移す
            // Preserve the subdirectory structure (pid_<PID>/segment-0.mp4, etc.) while moving into the destination
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(source, file);
                var destinationFile = Path.Combine(destination, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Move(file, destinationFile);
            }
            return destination;
        }

        // source配下のファイルとサブディレクトリを再帰的に削除する。source自体は残す
        // Recursively removes files and subdirectories under source; source itself is left intact
        private static void ClearDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            foreach (var subDirectory in Directory.GetDirectories(directory)) Directory.Delete(subDirectory, true);
        }
    }
}
