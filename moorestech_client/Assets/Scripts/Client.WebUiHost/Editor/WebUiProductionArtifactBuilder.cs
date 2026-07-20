#if UNITY_EDITOR
using System;
using System.Diagnostics;
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
            RunPnpmBuild();
            StageArtifact();
        }

        private static void RunPnpmBuild()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = WebUiPaths.PnpmBinary,
                Arguments = "build",
                WorkingDirectory = WebUiPaths.WebuiRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var nodeDirectory = Path.GetDirectoryName(WebUiPaths.NodeBinary);
            startInfo.Environment["PATH"] = $"{nodeDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";

            // 外部ビルド失敗を隔離する
            // pnpm is an external-process boundary, so convert launch failures into build failures
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                throw new BuildFailedException($"Web UI pnpm build could not start: {e.GetBaseException().Message}");
            }
            if (process == null) throw new BuildFailedException("Web UI pnpm build returned no process");
            using (process)
            {
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException($"Web UI pnpm build failed with exit code {process.ExitCode}");
            }
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
