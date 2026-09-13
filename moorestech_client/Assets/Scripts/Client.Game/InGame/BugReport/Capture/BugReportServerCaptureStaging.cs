using System;
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
            if (string.IsNullOrEmpty(snapshotDirectory)) return Failed("サーバーのスナップショット置き場が分からなかった");

            // ディスクは外部資源なので隔離する。退避先を作れなければ確保そのものが成立しないため理由を残して丸ごと諦める
            // Disk is an external resource and is isolated here; failing to create the staging root ends the capture, so the reason is recorded
            if (!TryResetDirectory(stagingDirectory, out var resetError)) return Failed($"退避先を用意できなかった: {resetError}");

            var stagedSnapshots = StageFiles(snapshotFileNames);
            var stagedPacketLogs = StageFiles(packetLogFileNames);
            return new StagedServerCapture(stagingDirectory, stagedSnapshots, stagedPacketLogs, missing);

            #region Internal

            List<string> StageFiles(IReadOnlyList<string> fileNames)
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

            bool TryResetDirectory(string directory, out string error)
            {
                error = null;

                // ディスクは外部資源。置き場を作れない理由を残さないと、空の箱が届いた理由がどこにも残らない
                // Disk is an external resource; without recording why the directory failed, an empty box arrives unexplained
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
                catch (UnauthorizedAccessException e)
                {
                    error = e.Message;
                }
                Debug.LogError($"バグ報告: 記録の退避先を用意できませんでした directory:{directory} reason:{error}");
                return false;
            }

            bool TryCopy(string source, string destination, out string error)
            {
                error = null;

                // File.Copy はディスク境界。落ちた1本の理由をここで捕まえ、残りの記録は退避を続ける
                // File.Copy is the disk boundary; the failure of one file is caught here while the rest keep staging
                try
                {
                    File.Copy(source, destination, true);
                    return true;
                }
                catch (IOException e)
                {
                    error = e.Message;
                }
                catch (UnauthorizedAccessException e)
                {
                    error = e.Message;
                }
                Debug.LogError($"バグ報告: 記録を退避できませんでした source:{source} reason:{error}");
                return false;
            }

            StagedServerCapture Failed(string reason)
            {
                missing.Add(Missing("snapshots", reason));
                return new StagedServerCapture(null, new List<string>(), new List<string>(), missing);
            }

            MissingItem Missing(string item, string reason)
            {
                return new MissingItem { Item = item, Reason = reason };
            }

            #endregion
        }
    }
}
