using System;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // 確保1回ぶんの一時資源（退避したサーバー記録・スクリーンショット）の置き場
    // Holds one capture's temporary materials: the staged server records and the screenshot
    // 置き場をプロセスで固定すると、次のEscapeが送信中の資料を消してしまうため確保ごとに別の場所を作る
    // A per-process directory lets the next Escape delete materials still being sent, so each capture gets its own
    public static class BugReportCaptureWorkspace
    {
        private const string DirectoryPrefix = "capture_";

        // 送信前に落ちたプロセスの置き土産を消す猶予。並行するPlayModeの作業中の置き場を巻き込まない長さにする
        // Grace period for leftovers from a process that died before sending; long enough never to hit a parallel PlayMode's live directory
        private const double OrphanMaxAgeHours = 24;

        // 場所を決めるだけで実体は作らない。書き手（退避・スクリーンショット）が書くときに作るので空の置き場が残らない
        // Only the location is decided; the writers (staging, screenshot) create it when they write, so no empty directory is left behind
        public static string Create()
        {
            return Path.Combine(GameSystemPaths.BugReportDirectory, $"{DirectoryPrefix}{Guid.NewGuid():N}");
        }

        public static void Delete(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            TryDelete(directory);
        }

        // 新しい確保を始める時点で、送信中の1つを除く作業場はすべて死んでいる。名指しでなく掃き出す
        // Every workspace but the one still being sent is dead once a new capture starts, so they are swept rather than named
        // 差し替え後に終わった退避・スクリーンショットが古い置き場を作り直すため、名指しの削除だけでは取りこぼす
        // Staging or a screenshot that finishes after the swap recreates its old directory, which a named delete would miss
        public static void DeleteAllExcept(string keepDirectory)
        {
            var root = GameSystemPaths.BugReportDirectory;
            if (!Directory.Exists(root)) return;

            foreach (var directory in Directory.GetDirectories(root, $"{DirectoryPrefix}*"))
            {
                if (directory == keepDirectory) continue;
                TryDelete(directory);
            }
        }

        // 起動時に呼ぶ。送信前にプロセスが落ちると作業場は誰も片付けないのでここで刈り取る
        // Called at boot; a process that dies before sending leaves a workspace nobody else reclaims
        public static void CleanOrphans()
        {
            var root = GameSystemPaths.BugReportDirectory;
            if (!Directory.Exists(root)) return;

            var expiredBefore = DateTime.UtcNow.AddHours(-OrphanMaxAgeHours);
            foreach (var directory in Directory.GetDirectories(root, $"{DirectoryPrefix}*"))
            {
                if (expiredBefore < Directory.GetLastWriteTimeUtc(directory)) continue;
                Debug.Log($"バグ報告: 送信されなかった確保の作業場を片付けます directory:{directory}");
                TryDelete(directory);
            }
        }

        private static void TryDelete(string directory)
        {
            // ディスクは外部資源。片付けに失敗しても確保と送信は続けられるので理由だけ残して先へ進む
            // Disk is an external resource; a failed cleanup never blocks capture or sending, so only the reason is recorded
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"バグ報告: 確保の作業場を片付けられませんでした directory:{directory} reason:{e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"バグ報告: 確保の作業場を片付けられませんでした directory:{directory} reason:{e.Message}");
            }
        }
    }
}
