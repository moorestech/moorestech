#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Client.WebUiHost.Common;
using Client.WebUiHost.Static;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.WebUiHost.Editor
{
    /// <summary>
    /// Web UI成果物をPlayerへ同梱する
    /// Builds the web UI before Player build and stages dist with its manifest
    /// </summary>
    public class WebUiProductionArtifactBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // ツールチェーン不足はここで自動導入し、ビルドを自己完結させる
            // Auto-provision a missing toolchain here so the build is self-contained
            WebUiToolchainBootstrap.EnsureReady();
            RunPnpmBuild();
            StageArtifact();
        }

        private static void RunPnpmBuild()
        {
            var nodeDirectory = Path.GetDirectoryName(WebUiPaths.NodeBinary);
            var exitCode = EditorProcessRunner.Run(WebUiPaths.PnpmBinary, "build", WebUiPaths.WebuiRoot, nodeDirectory);
            if (exitCode != 0) throw new BuildFailedException($"Web UI pnpm build failed with exit code {exitCode}");
        }

        private static void StageArtifact()
        {
            var source = Path.Combine(WebUiPaths.WebuiRoot, "dist");
            var target = WebUiPaths.ProductionDistRoot;
            if (!Directory.Exists(source)) throw new BuildFailedException($"Web UI dist not found: {source}");

            // 配置先を置換して複製する
            // Replace the staging directory to avoid stale hashed assets, then copy every file
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            foreach (var sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = sourceFile.Substring(source.Length + 1);
                var targetFile = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(sourceFile, targetFile, true);
            }

            var files = Directory.GetFiles(target, "*", SearchOption.AllDirectories)
                .Select(path => new WebUiArtifactFile { path = ToRelativePath(target, path), sha256 = ComputeSha256(path) })
                .OrderBy(file => file.path, StringComparer.Ordinal)
                .ToArray();
            var manifest = new WebUiArtifactManifest
            {
                contractVersion = WebUiBuildContract.ContractVersion,
                buildVersion = Application.version,
                files = files,
            };
            File.WriteAllText(Path.Combine(target, WebUiBuildContract.ManifestFileName), JsonUtility.ToJson(manifest, true));
            UnityEngine.Debug.Log($"[WebUiHost] staged production artifact: {files.Length} files at {target}");
        }

        private static string ToRelativePath(string root, string path)
        {
            return path.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
