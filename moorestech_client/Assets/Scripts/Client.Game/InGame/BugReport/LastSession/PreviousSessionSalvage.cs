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

            artifacts.CrashDumpFiles = CrashDumpLocator.FindDumpFiles();
            if (artifacts.CrashDumpFiles.Count == 0) artifacts.Missing.Add(new MissingItem { Item = "crashDump", Reason = $"クラッシュダンプが見つからない（探索先: {string.Join(", ", CrashDumpLocator.CandidateRoots())}）" });

            return artifacts;
        }

        // 中身のあるディレクトリだけを退避先へ移す。空・不在は欠損として残し、呼び出し側は null で受ける
        // Moves only a non-empty directory; empty or absent is recorded as missing and returned as null
        private static string MoveFilesInto(string source, string destination, string item, PreviousSessionArtifacts artifacts)
        {
            if (source == null || !Directory.Exists(source))
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が無い: {source}" });
                return null;
            }
            var files = Directory.GetFiles(source);
            if (files.Length == 0)
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が空: {source}" });
                return null;
            }

            ClearDirectory(destination);
            Directory.CreateDirectory(destination);
            foreach (var file in files) File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
            return destination;
        }

        private static void ClearDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        }
    }
}
