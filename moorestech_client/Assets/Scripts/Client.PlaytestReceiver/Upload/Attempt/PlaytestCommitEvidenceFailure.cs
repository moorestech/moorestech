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
            if (manifest["isEditor"]?.Type == JTokenType.Boolean && (bool)manifest["isEditor"] && !File.Exists(status))
                skipped.Add(new PlaytestSkippedFile("repo/commit-evidence", "Editorで元SHAの資料補完が必要です", 0));
            var statuses = File.Exists(status) ? File.ReadAllLines(status).ToList() : new System.Collections.Generic.List<string>();
            foreach (var line in statuses.ToArray())
            {
                if (!line.EndsWith(": created", System.StringComparison.Ordinal)) continue; // silent-ok: 作成済み以外に実体は不要 / 既存の欠損理由はmanifestに残る
                var relative = BugReportBundleLayout.RepositoryDirectoryName + "/" + line.Substring(0, line.Length - ": created".Length);
                if (BugReportBoxFilePolicy.IsCommitBundle(relative) && !File.Exists(Path.Combine(directory, relative)))
                    skipped.Add(new PlaytestSkippedFile(relative, "created資料が消失しました", 0));
            }
            if (skipped.Count == 0) return null; // silent-ok: 資料の配送障害無し / 通常配送を継続
            var reason = string.Join("; ", skipped.Select(file => file.Path + ": " + file.Reason));
            // 送れないコミット資料を成功扱いにせず、画面の失敗件数へ残す
            // Keep unsendable commit evidence in the UI failure count rather than reporting success
            File.WriteAllText(Path.Combine(directory, PlaytestOutboxScanner.FailedMarker), reason);
            Debug.LogWarning($"[PlaytestReceiver] commit evidence cannot be delivered; box held: {Path.GetFileName(directory)}: {reason}");
            if (File.Exists(Path.Combine(directory, PlaytestOutboxScanner.DeclaredMarker)))
            {
                Debug.LogWarning("[PlaytestReceiver] preserving already-declared bytes; evidence failure is recorded in UPLOAD_FAILED");
                return reason;
            }
            if (manifest["missing"] is not JArray missing)
                throw new Newtonsoft.Json.JsonSerializationException("manifest.missing must be an array to record undeliverable evidence");
            foreach (var file in skipped)
                missing.Add(new JObject { ["item"] = file.Path, ["reason"] = "配送不能: " + file.Reason });
            File.WriteAllText(path, manifest.ToString());
            foreach (var file in skipped)
            {
                var label = Path.GetFileName(file.Path) + ": ";
                statuses.RemoveAll(line => line.StartsWith(label, System.StringComparison.Ordinal));
                statuses.Add(label + "failed-delivery (" + file.Reason + ")");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(status));
            File.WriteAllLines(status, statuses);
            return reason;
        }
    }
}
