using System;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // outbox の配置規則はここだけが持つ。READY は「manifest まで書き終えた」合図で、運搬側はこれが無い箱を触らない
    // Owns the outbox layout; READY signals the manifest is written, and the shipper ignores boxes without it
    public static class BugReportOutbox
    {
        public const string ReadyMarkerFileName = "READY";

        public static string DefaultRootDirectory => GameSystemPaths.BugReportOutboxDirectory;

        // 箱の名前は「時刻＋短いid」の1規約。進行記録など別ツリーの置き場も同じ規約を共有するため root を引数で受ける
        // One naming rule of "timestamp + short id"; the root is an argument so other trees such as the progress records share the same rule
        public static string CreateBundleDirectory(string rootDirectory, DateTime now, string shortId)
        {
            var directory = Path.Combine(rootDirectory, $"{now:yyyyMMdd_HHmmss}_{shortId}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        // 同じ秒に2箱できても衝突しないための短いid。長さと綴りをここへ集め、呼び出し側でGuidを刻まない
        // The short id that keeps two boxes in the same second apart; its length and spelling live here so callers never slice a Guid
        public static string CreateShortId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        // 箱を閉じる唯一の手順。manifest を書いてから READY を置く順序はここでしか表現しない
        // The one way to close a box; only here is the order "write the manifest, then place READY" expressed
        // ディスクは外部資源。閉じられなかった箱は運搬されないので、握った失敗は必ず開発者ログへ理由を残す
        // Disk is an external resource; a box that could not be closed is never shipped, so every swallowed failure logs its reason
        public static bool TryFinishBundle(string bundleDirectory, BugReportManifest manifest)
        {
            try
            {
                File.WriteAllText(Path.Combine(bundleDirectory, BugReportBundleLayout.ManifestFileName), manifest.ToJson());
                MarkReady(bundleDirectory);
                Debug.Log($"プレイ報告の箱を書きました kind:{manifest.Kind} {bundleDirectory} missing:{manifest.Missing.Count}");
                return true;
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"プレイ報告の箱のmanifestを書けませんでした（この箱は運搬されません） kind:{manifest.Kind} {bundleDirectory}: {e.Message}");
                return false;
            }
        }

        public static void MarkReady(string bundleDirectory)
        {
            File.WriteAllText(Path.Combine(bundleDirectory, ReadyMarkerFileName), "");
        }
    }
}
