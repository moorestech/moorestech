using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // Escape時点のサーバー記録を実体ごと退避する。サーバーは記入中も剪定を進めるので名前を控えるだけでは間に合わない
    // Stages the Escape-moment server records as real files; the server keeps pruning while the user types, so names alone arrive too late
    public static class BugReportServerCaptureStaging
    {
        public static StagedServerCapture Stage(string stagingDirectory, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            var missing = new List<MissingItem>();
            if (string.IsNullOrEmpty(snapshotDirectory))
            {
                return Failed(missing, "サーバーのスナップショット置き場が分からなかった");
            }

            // ディスクは外部資源なので隔離する。退避先を作れなければ確保そのものが成立しないため理由を残して丸ごと諦める
            // Disk is an external resource and is isolated here; failing to create the staging root ends the capture, so the reason is recorded
            if (!TryResetDirectory(stagingDirectory, out var resetError))
            {
                return Failed(missing, $"退避先を用意できなかった: {resetError}");
            }

            var stagedSnapshots = StageFiles(stagingDirectory, snapshotDirectory, snapshotFileNames, missing);
            var stagedPacketLogs = StageFiles(stagingDirectory, snapshotDirectory, packetLogFileNames, missing);
            return new StagedServerCapture(stagingDirectory, stagedSnapshots, stagedPacketLogs, missing);
        }

        private static List<string> StageFiles(string stagingDirectory, string snapshotDirectory, IReadOnlyList<string> fileNames, List<MissingItem> missing)
        {
            var staged = new List<string>();
            foreach (var name in fileNames)
            {
                var source = Path.Combine(snapshotDirectory, name);
                if (!File.Exists(source))
                {
                    missing.Add(Missing(name, "確保しようとした時点でスナップショット置き場に無かった"));
                    continue;
                }

                // File.Copy はディスク境界。1本の失敗で残りの記録まで落とさないようここで隔離し理由を残す
                // File.Copy is the disk boundary; isolate it here with a reason so one failure never drops the remaining records
                if (!TryCopy(source, Path.Combine(stagingDirectory, name), out var copyError))
                {
                    missing.Add(Missing(name, $"確保時の退避に失敗した: {copyError}"));
                    continue;
                }
                staged.Add(name);
            }
            return staged;
        }

        private static bool TryResetDirectory(string directory, out string error)
        {
            error = null;
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                Directory.CreateDirectory(directory);
                return true;
            }
            catch (IOException e)
            {
                error = e.Message;
            }
            catch (System.UnauthorizedAccessException e)
            {
                error = e.Message;
            }
            Debug.LogError($"バグ報告: 記録の退避先を用意できませんでした directory:{directory} reason:{error}");
            return false;
        }

        private static bool TryCopy(string source, string destination, out string error)
        {
            error = null;
            try
            {
                File.Copy(source, destination, true);
                return true;
            }
            catch (IOException e)
            {
                error = e.Message;
            }
            catch (System.UnauthorizedAccessException e)
            {
                error = e.Message;
            }
            Debug.LogError($"バグ報告: 記録を退避できませんでした source:{source} reason:{error}");
            return false;
        }

        private static StagedServerCapture Failed(List<MissingItem> missing, string reason)
        {
            missing.Add(Missing("snapshots", reason));
            return new StagedServerCapture(null, new List<string>(), new List<string>(), missing);
        }

        private static MissingItem Missing(string item, string reason)
        {
            return new MissingItem { Item = item, Reason = reason };
        }
    }
}
