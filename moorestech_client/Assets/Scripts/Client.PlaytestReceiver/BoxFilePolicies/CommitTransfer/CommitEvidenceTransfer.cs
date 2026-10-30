using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;

namespace Client.PlaytestReceiver.BoxFilePolicies.CommitTransfer
{
    internal static class CommitEvidenceTransfer
    {
        internal const string IndexPath = "repo/commit-transfer.json";
        internal const string PartsDirectory = "__attachments";

        internal static bool IsCapacityHold(string directory)
        {
            if (File.Exists(Path.Combine(directory, PlaytestOutboxScanner.DeclaredMarker))) return false; // silent-ok: 宣言済みは変更不可 / 保留が画面に残る
            var path = Path.Combine(directory, PlaytestOutboxScanner.FailedMarker);
            if (!File.Exists(path)) return false; // silent-ok: 保留なし / 通常配送へ進む
            return System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path),
                @"\Arepo/(?:commits|master-commits)\.bundle: (?:too-large|bundle-too-large|too-many-files)(?:; repo/(?:commits|master-commits)\.bundle: (?:too-large|bundle-too-large|too-many-files))*\z");
        }

        internal static void ClearCapacityHold(string directory)
        {
            if (!IsCapacityHold(directory)) return; // silent-ok: 他の恒久失敗は解除しない / 保留印と画面に残る
            var path = Path.Combine(directory, "manifest.json");
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (manifest["missing"] is not JArray missing) throw new Newtonsoft.Json.JsonSerializationException("manifest.missing must be an array");
            foreach (var item in missing.Where(item => item is JObject && BugReportBoxFilePolicy.IsCommitBundle((string)item["item"]) &&
                         System.Text.RegularExpressions.Regex.IsMatch((string)item["reason"] ?? "", @"\A配送不能: (too-large|bundle-too-large|too-many-files)\z")).ToArray()) item.Remove();
            File.WriteAllText(path, manifest.ToString());
            PlaytestFailureMarkers.Clear(directory);
        }

        internal static IReadOnlyList<PlaytestOutboxBox> Prepare(PlaytestOutboxBox box)
        {
            var indexPath = Path.Combine(box.Directory, IndexPath);
            if (!File.Exists(indexPath))
            {
                var payloads = PlaytestOutboxScanner.ListPayloadFiles(box.Directory);
                var bundles = payloads.Where(path => BugReportBoxFilePolicy.IsCommitBundle(PlaytestOutboxScanner.ToRelativePath(box.Directory, path))).ToArray();
                if (bundles.Length == 0 || (!bundles.Any(path => PlaytestReceiverConfig.MaxFileBytes < new FileInfo(path).Length) &&
                    payloads.Sum(path => new FileInfo(path).Length) <= PlaytestReceiverConfig.MaxBundleBytes && payloads.Count <= PlaytestReceiverConfig.MaxBundleFiles))
                    return Array.Empty<PlaytestOutboxBox>(); // silent-ok: 通常宣言の上限内 / 配送時にも上限を検証
                if (File.Exists(Path.Combine(box.Directory, PlaytestOutboxScanner.DeclaredMarker)))
                {
                    // 既宣言は分割せず、通常の必須資料検証で保留理由を記録する
                    // Preserve declared evidence and let required-file validation record the hold
                    UnityEngine.Debug.LogWarning("[PlaytestReceiver] preserving declared commit evidence; validating the original declaration without multipart conversion");
                    return Array.Empty<PlaytestOutboxBox>();
                }
                WriteIndex(box.Directory, bundles, indexPath);
            }

            // 宣言前に全断片を完成させ、再送は同じ計画とキーを再利用する
            // Finish all parts before declaring; retries reuse exactly the same plan and keys
            var plan = JArray.Parse(File.ReadAllText(indexPath));
            var attachments = new List<PlaytestOutboxBox>();
            foreach (var bundle in plan)
            {
                if (bundle is not JObject || bundle["parts"] is not JArray parts)
                    throw new Newtonsoft.Json.JsonSerializationException("Invalid commit transfer bundle");
                foreach (var part in parts)
                {
                var relative = part is JObject ? (string)part["path"] : null;
                if (relative == null || !System.Text.RegularExpressions.Regex.IsMatch(relative, "^__attachments/[0-9]{6}/chunk$"))
                    throw new IOException("Invalid commit transfer part path: " + relative);
                var directory = Path.GetDirectoryName(relative);
                attachments.Add(new PlaytestOutboxBox(Path.Combine(box.Directory, directory), box.BundleId + "/" + directory.Replace('\\', '/'),
                    box.Kind, new CommitTransferPartPolicy()));
                }
            }
            // 分割計画完成後、宣言の長さを測る前に容量だけの保留を解く
            // Once the plan exists, clear capacity-only holds before measuring declaration sizes
            ClearCapacityHold(box.Directory);
            return attachments;
        }

        private static void WriteIndex(string box, string[] bundles, string indexPath)
        {
            var plan = new JArray();
            var partNumber = 0;
            var buffer = new byte[64 * 1024];
            foreach (var bundle in bundles.OrderBy(path => path, StringComparer.Ordinal))
            {
                using var source = File.OpenRead(bundle);
                var parts = new JArray();
                // 各転送は既存の単体・箱・ファイル数上限内に収める
                // Every transfer stays inside the existing per-file, per-box and file-count limits
                var partLimit = Math.Min(PlaytestReceiverConfig.MaxFileBytes, PlaytestReceiverConfig.MaxBundleBytes);
                while (source.Position < source.Length)
                {
                    var relative = PartsDirectory + "/" + partNumber.ToString("D6") + "/chunk";
                    partNumber++;
                    var path = Path.Combine(box, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var remaining = Math.Min(partLimit, source.Length - source.Position);
                    var bytes = remaining;
                    using (var output = File.Create(path + ".tmp"))
                    {
                        while (0 < remaining)
                        {
                            var count = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (count == 0) throw new IOException("Commit bundle ended while splitting: " + bundle);
                            output.Write(buffer, 0, count);
                            remaining -= count;
                        }
                    }
                    if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                    else File.Move(path + ".tmp", path);
                    using var hash = SHA256.Create();
                    using var input = File.OpenRead(path);
                    var digest = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                    parts.Add(new JObject { ["path"] = relative, ["bytes"] = bytes, ["sha256"] = digest });
                }
                plan.Add(new JObject { ["path"] = PlaytestOutboxScanner.ToRelativePath(box, bundle), ["bytes"] = source.Length, ["parts"] = parts });
            }
            File.WriteAllText(indexPath + ".tmp", plan.ToString());
            File.Move(indexPath + ".tmp", indexPath);
        }
    }
}
