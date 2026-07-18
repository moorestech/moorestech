using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Client.WebUiHost.Common;
using UnityEngine;

namespace Client.WebUiHost.Static
{
    public static class WebUiArtifactValidator
    {
        public static bool TryValidate(string rootPath, out string failure)
        {
            // 外部成果物のI/O障害を隔離する
            // The artifact is an external-file boundary, so convert every I/O failure into startup failure
            try
            {
                return Validate(rootPath, out failure);
            }
            catch (Exception e)
            {
                return Fail($"artifact load failed: {e.GetBaseException().Message}", out failure);
            }
        }

        private static bool Validate(string rootPath, out string failure)
        {
            failure = "";
            var manifestPath = Path.Combine(rootPath, WebUiBuildContract.ManifestFileName);
            if (!File.Exists(manifestPath)) return Fail($"manifest not found: {manifestPath}", out failure);

            var manifest = JsonUtility.FromJson<WebUiArtifactManifest>(File.ReadAllText(manifestPath));

            if (manifest == null || manifest.contractVersion != WebUiBuildContract.ContractVersion)
                return Fail($"contract mismatch: expected {WebUiBuildContract.ContractVersion}, got {manifest?.contractVersion ?? "null"}", out failure);
            if (manifest.buildVersion != Application.version)
                return Fail($"build version mismatch: expected {Application.version}, got {manifest.buildVersion ?? "null"}", out failure);
            if (manifest.files == null || manifest.files.Length == 0) return Fail("manifest has no files", out failure);

            // 正規manifest項目だけを照合する
            // Reject duplicates and directory traversal, then verify only canonical manifest entries
            var comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var seen = new HashSet<string>(comparer);
            foreach (var entry in manifest.files)
            {
                if (!TryResolveFile(rootPath, entry?.path, out var filePath) || !seen.Add(entry.path))
                    return Fail($"invalid manifest path: {entry?.path ?? "null"}", out failure);
                if (!File.Exists(filePath)) return Fail($"artifact file missing: {entry.path}", out failure);
                if (!HashMatches(filePath, entry.sha256)) return Fail($"artifact hash mismatch: {entry.path}", out failure);
            }

            if (!seen.Contains("index.html")) return Fail("manifest does not contain index.html", out failure);
            var actualFileCount = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories).Length - 1;
            if (actualFileCount != seen.Count) return Fail("artifact contains files not declared by manifest", out failure);
            return true;
        }

        public static bool TryResolveFile(string rootPath, string relativePath, out string filePath)
        {
            filePath = "";
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) return false;
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!candidate.StartsWith(root, comparison)) return false;
            filePath = candidate;
            return true;
        }

        private static bool HashMatches(string filePath, string expected)
        {
            if (string.IsNullOrEmpty(expected)) return false;
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
