using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Starter.Playtest
{
    internal static class PlaytestExistingReportEvidence
    {
        internal static void Complete(PlaytestOutboxDirectories directories, string repositoryRoot, string masterDataRoot)
        {
            foreach (var box in PlaytestOutboxScanner.ScanPending(directories, PlaytestOutboxScope.ReportsOnly))
            {
                string failure;
                // 旧箱の外部JSON・ディスク・git境界。1箱の失敗を他の箱へ波及させない
                // Isolate external legacy JSON, disk and git failures to the affected box
                try
                {
                    failure = BugReportCommitBundles.CompleteExisting(box.Directory, repositoryRoot, masterDataRoot,
                        Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    failure = "旧箱の資料を補完できません: " + error.Message;
                }
                if (failure == null) continue; // silent-ok: 元SHAの資料補完済み / 後続の送信が結果を表示
                // 印は配送画面へ残る欠損表明。既宣言のmanifestを書き換えない
                // The marker exposes the missing evidence in delivery UI without changing a declared manifest
                Debug.LogWarning($"[PlaytestReceiver] holding legacy report {box.BundleId}: {failure}");
                // 保留印の保存も外部ディスク境界に含め、他箱への波及を防ぐ
                // Include hold-marker writes in the external disk boundary to isolate boxes
                try
                {
                    File.WriteAllText(Path.Combine(box.Directory, PlaytestOutboxScanner.FailedMarker), failure);
                    if (File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker))) continue; // silent-ok: 既宣言本文は不変 / 失敗印と画面で保留を通知
                    var path = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                    var manifest = JObject.Parse(File.ReadAllText(path));
                    if (manifest["missing"] is not JArray missing) throw new JsonSerializationException("manifest.missing is not an array");
                    missing.Add(new JObject { ["item"] = "repo/commit-evidence", ["reason"] = failure });
                    File.WriteAllText(path, manifest.ToString());
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] could not record legacy evidence failure; pre-upload validation will hold this box: {box.BundleId}: {error.Message}");
                }
            }
        }
    }
}
