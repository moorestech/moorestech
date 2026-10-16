using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    internal static class PlaytestCommitEvidenceFailure
    {
        internal static bool IsCommitBundle(string path)
        {
            return path == BugReportBundleLayout.RepositoryDirectoryName + "/" + BugReportBundleLayout.CommitsBundleFileName ||
                   path == BugReportBundleLayout.RepositoryDirectoryName + "/" + BugReportBundleLayout.MasterCommitsBundleFileName;
        }

        internal static void Record(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration)
        {
            if (box.Kind != PlaytestUploadKind.Report) return; // silent-ok: 進行記録にgit資料は無い / 報告だけを検査
            var skipped = declaration.Skipped.Where(file => IsCommitBundle(file.Path)).ToArray();
            if (skipped.Length == 0) return; // silent-ok: コミット資料の欠損無し / 通常配送を継続
            var reason = string.Join("; ", skipped.Select(file => file.Path + ": " + file.Reason));
            // 送れないコミット資料を成功扱いにせず、画面の失敗件数へ残す
            // Keep unsendable commit evidence in the UI failure count rather than reporting success
            File.WriteAllText(Path.Combine(box.Directory, PlaytestOutboxScanner.FailedMarker), reason);
            Debug.LogWarning($"[PlaytestReceiver] commit evidence cannot be delivered; box held: {box.BundleId}: {reason}");
            if (File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker)))
            {
                Debug.LogWarning("[PlaytestReceiver] preserving already-declared bytes; evidence failure is recorded in UPLOAD_FAILED");
                return;
            }
            var path = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (manifest["missing"] is not JArray missing)
                throw new Newtonsoft.Json.JsonSerializationException("manifest.missing must be an array to record undeliverable evidence");
            foreach (var file in skipped)
                missing.Add(new JObject { ["item"] = file.Path, ["reason"] = "配送不能: " + file.Reason });
            File.WriteAllText(path, manifest.ToString());
            var status = Path.Combine(box.Directory, BugReportBundleLayout.RepositoryDirectoryName, BugReportBundleLayout.BundleStatusFileName);
            var statuses = File.Exists(status) ? File.ReadAllLines(status).ToList() : new System.Collections.Generic.List<string>();
            foreach (var file in skipped)
            {
                var label = Path.GetFileName(file.Path) + ": ";
                statuses.RemoveAll(line => line.StartsWith(label, System.StringComparison.Ordinal));
                statuses.Add(label + "failed-delivery (" + file.Reason + ")");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(status));
            File.WriteAllLines(status, statuses);
        }
    }
}
