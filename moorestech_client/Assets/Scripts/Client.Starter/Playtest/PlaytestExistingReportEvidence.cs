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
            foreach (var box in PlaytestOutboxScanner.ScanForEvidence(directories))
            {
                string failure;
                // 旧箱の外部JSON・ディスク・git境界。1箱の失敗を他の箱へ波及させない
                // Isolate external legacy JSON, disk and git failures to the affected box
                try
                {
                    using var boxLock = PlaytestOutboxAccess.AcquireBox(box.Directory);
                    var wasCompletionHold = PlaytestOutboxScanner.IsCompletionHold(box.Directory);
                    failure = BugReportCommitBundles.CompleteExisting(box.Directory, repositoryRoot, masterDataRoot,
                        Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker));
                    if (failure == null && wasCompletionHold)
                    {
                        var manifestPath = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                        var restored = JObject.Parse(File.ReadAllText(manifestPath));
                        var missing = (JArray)restored["missing"];
                        foreach (var item in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(missing, item => (string)item["item"] == "repo/commit-evidence"))) item.Remove();
                        File.WriteAllText(manifestPath, restored.ToString());
                        PlaytestFailureMarkers.Clear(box.Directory);
                    }
                    if (failure != null) RecordFailure();
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    failure = "旧箱の資料を補完できません: " + error.Message;
                }
                if (failure == null) continue; // silent-ok: 補完済み / 後続配送が結果を表示
                Debug.LogWarning($"[PlaytestReceiver] holding legacy report {box.BundleId}: {failure}");

                void RecordFailure()
                {
                    File.WriteAllText(Path.Combine(box.Directory, PlaytestOutboxScanner.FailedMarker), PlaytestOutboxScanner.CompletionHoldPrefix + failure);
                    if (File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker))) return; // silent-ok: 既宣言の本文は不変 / 保留印で通知
                    var path = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                    var manifest = JObject.Parse(File.ReadAllText(path));
                    if (manifest["missing"] is not JArray missing) throw new JsonSerializationException("manifest.missing is not an array");
                    missing.Add(new JObject { ["item"] = "repo/commit-evidence", ["reason"] = failure });
                    File.WriteAllText(path, manifest.ToString());
                }
            }
        }
    }
}
