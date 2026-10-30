using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.BoxFilePolicies
{
    internal static class PlaytestCommitEvidenceFailure
    {
        internal static string ValidateAndRecord(string directory, PlaytestBoxDeclaration declaration)
        {
            var path = Path.Combine(directory, BugReportBundleLayout.ManifestFileName);
            var manifest = JObject.Parse(File.ReadAllText(path));
            var status = Path.Combine(directory, BugReportBundleLayout.RepositoryDirectoryName, BugReportBundleLayout.BundleStatusFileName);
            var skipped = declaration.Skipped.Where(file => BugReportBoxFilePolicy.IsCommitBundle(file.Path)).ToList();
            // 旧Editor箱と作成後に消えた資料を、全HTTP入口で保留する
            // Hold legacy Editor boxes and evidence lost after creation at every HTTP entry
            var statuses = File.Exists(status) ? File.ReadAllLines(status).ToList() : new System.Collections.Generic.List<string>();
            var completed = new[] { BugReportBundleLayout.CommitsBundleFileName, BugReportBundleLayout.MasterCommitsBundleFileName }
                .All(name => statuses.Any(line => line.StartsWith(name + ": ", System.StringComparison.Ordinal) && !line.Contains(": failed-delivery")));
            if (!completed && manifest["isEditor"]?.Type != JTokenType.Boolean)
                skipped.Add(new PlaytestSkippedFile("repo/commit-evidence", "manifest.isEditor が不明のため元repoを特定できません", 0));
            else if (!completed && (bool)manifest["isEditor"])
                skipped.Add(new PlaytestSkippedFile("repo/commit-evidence", "Editorで元SHAの資料補完が必要です", 0));
            foreach (var line in statuses.ToArray())
            {
                if (!line.EndsWith(": created", System.StringComparison.Ordinal) && !line.Contains(": failed-delivery")) continue; // silent-ok: 作成失敗には実体不要 / manifestに欠損理由が残る
                var relative = BugReportBundleLayout.RepositoryDirectoryName + "/" + line.Substring(0, line.IndexOf(": ", System.StringComparison.Ordinal));
                if (BugReportBoxFilePolicy.IsCommitBundle(relative) && !File.Exists(Path.Combine(directory, relative)))
                    skipped.Add(new PlaytestSkippedFile(relative, "created資料が消失しました", 0));
            }
            if (skipped.Count == 0) return null; // silent-ok: 資料の配送障害無し / 通常配送を継続
            var reason = string.Join("; ", skipped.Select(file => file.Path + ": " + file.Reason));
            // 送れないコミット資料を成功扱いにせず、画面の失敗件数へ残す
            // Keep unsendable commit evidence in the UI failure count rather than reporting success
            var completionOnly = skipped.Count == 1 && skipped[0].Path == "repo/commit-evidence" &&
                                 manifest["isEditor"]?.Type == JTokenType.Boolean && (bool)manifest["isEditor"];
            File.WriteAllText(Path.Combine(directory, PlaytestOutboxScanner.FailedMarker),
                completionOnly ? PlaytestOutboxScanner.CompletionHoldPrefix + reason : reason);
            Debug.LogWarning($"[PlaytestReceiver] commit evidence cannot be delivered; box held: {Path.GetFileName(directory)}: {reason}");
            if (File.Exists(Path.Combine(directory, PlaytestOutboxScanner.DeclaredMarker)))
            {
                Debug.LogWarning("[PlaytestReceiver] preserving already-declared bytes; evidence failure is recorded in UPLOAD_FAILED");
                return reason;
            }
            if (manifest["missing"] is not JArray missing)
                throw new Newtonsoft.Json.JsonSerializationException("manifest.missing must be an array to record undeliverable evidence");
            // 同じ item と reason が既にあれば足さない。再実行で同じ行が増えない
            // Skip an item+reason already present so reruns never duplicate the row
            foreach (var file in skipped)
            {
                var recordedReason = "配送不能: " + file.Reason;
                if (missing.Any(item => item is JObject row && (string)row["item"] == file.Path && (string)row["reason"] == recordedReason)) continue; // silent-ok: 既に同じ欠損が記録済み / manifest.missing に残っている
                missing.Add(new JObject { ["item"] = file.Path, ["reason"] = recordedReason });
            }
            File.WriteAllText(path, manifest.ToString());
            foreach (var file in skipped)
            {
                var label = "delivery/" + Path.GetFileName(file.Path) + ": ";
                statuses.RemoveAll(line => line.StartsWith(label, System.StringComparison.Ordinal));
                statuses.Add(label + "failed-delivery (" + file.Reason + ")");
            }
            // 補完結果は保持し、配送失敗だけで補完済みの証拠を作らない
            // Preserve completion results and never create completion evidence from a delivery failure
            if (File.Exists(status)) File.WriteAllLines(status, statuses);
            return reason;
        }
    }
}
