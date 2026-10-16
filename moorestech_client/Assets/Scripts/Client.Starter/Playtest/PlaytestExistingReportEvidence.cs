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
        internal static void Complete(PlaytestOutboxDirectories directories)
        {
            foreach (var box in PlaytestOutboxScanner.ScanPending(directories, PlaytestOutboxScope.ReportsOnly))
            {
                var status = Path.Combine(box.Directory, BugReportBundleLayout.RepositoryDirectoryName, BugReportBundleLayout.BundleStatusFileName);
                if (File.Exists(status)) continue; // silent-ok: 新形式の箱は作成時に資料を確保済み / 配送時の上限検査へ渡す
                string failure;
                // 旧箱の外部JSON・ディスク・git境界。1箱の失敗を他の箱へ波及させない
                // Isolate external legacy JSON, disk and git failures to the affected box
                try
                {
                    var path = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                    var manifest = JObject.Parse(File.ReadAllText(path));
                    if (manifest["isEditor"]?.Type == JTokenType.Boolean && !(bool)manifest["isEditor"])
                        continue; // silent-ok: 配布ビルド箱はgit補完対象外 / buildInfoが出所を示す
                    failure = File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker))
                        ? "既宣言の旧箱には資料を追加できません。元箱を維持して開発者へ復旧を依頼してください"
                        : BugReportCommitBundles.CompleteExisting(box.Directory, RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    failure = "旧箱の資料を補完できません: " + error.Message;
                }
                if (failure == null) continue; // silent-ok: 元SHAの資料補完済み / 後続の送信が結果を表示
                // 印は配送画面へ残る欠損表明。既宣言のmanifestを書き換えない
                // The marker exposes the missing evidence in delivery UI without changing a declared manifest
                Debug.LogWarning($"[PlaytestReceiver] holding legacy report {box.BundleId}: {failure}");
                File.WriteAllText(Path.Combine(box.Directory, PlaytestOutboxScanner.FailedMarker), failure);
                if (File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker))) continue; // silent-ok: 宣言済み本文は不変 / 保留理由は失敗印と画面に残す
                // 欠損追記も外部JSON・ディスク境界。書けなくても失敗印で保留を維持する
                // Recording missing evidence is an external JSON/disk boundary; the failure marker preserves the hold
                try
                {
                    var path = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                    var manifest = JObject.Parse(File.ReadAllText(path));
                    if (manifest["missing"] is not JArray missing) throw new JsonSerializationException("manifest.missing is not an array");
                    missing.Add(new JObject { ["item"] = "repo/commit-evidence", ["reason"] = failure });
                    File.WriteAllText(path, manifest.ToString());
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] could not append manifest.missing; UPLOAD_FAILED retains the hold: {box.BundleId}: {error.Message}");
                }
            }
        }
    }
}
