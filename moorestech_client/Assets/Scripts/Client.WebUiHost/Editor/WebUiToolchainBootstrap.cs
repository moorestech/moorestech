#if UNITY_EDITOR
using System.IO;
using System.Runtime.InteropServices;
using Client.WebUiHost.Common;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Editor
{
    /// <summary>
    /// Web UIビルドに必要なNode/pnpmと依存パッケージを揃える
    /// Ensures the Node/pnpm toolchain and dependencies needed for the Web UI build
    /// </summary>
    public static class WebUiToolchainBootstrap
    {
        public static void EnsureReady()
        {
            EnsureToolchain();
            EnsureNodeModules();
        }

        private static void EnsureToolchain()
        {
            if (File.Exists(WebUiPaths.NodeBinary) && File.Exists(WebUiPaths.PnpmBinary)) return;

            // 不足時はOS対応のsetupスクリプトで自動導入する
            // Auto-install via the OS-specific setup script when the toolchain is missing
            var webRoot = Path.GetFullPath(Path.Combine(WebUiPaths.WebuiRoot, ".."));
            Debug.Log("[WebUiToolchainBootstrap] node/pnpm not found; running Web UI setup...");
            var exitCode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? EditorProcessRunner.Run("powershell", $"-ExecutionPolicy Bypass -File \"{Path.Combine(webRoot, "setup.ps1")}\"", webRoot, "")
                : EditorProcessRunner.Run("/bin/bash", $"\"{Path.Combine(webRoot, "setup.sh")}\"", webRoot, "");
            if (exitCode != 0)
            {
                throw new BuildFailedException($"Web UI toolchain setup failed with exit code {exitCode} (see {webRoot}/setup.sh or setup.ps1)");
            }

            if (!File.Exists(WebUiPaths.NodeBinary) || !File.Exists(WebUiPaths.PnpmBinary))
            {
                throw new BuildFailedException($"Web UI toolchain is still missing after setup: {WebUiPaths.PnpmBinary}");
            }
        }

        private static void EnsureNodeModules()
        {
            if (Directory.Exists(Path.Combine(WebUiPaths.WebuiRoot, "node_modules"))) return;

            // lockfile固定で依存を導入する
            // Install dependencies pinned to the lockfile
            Debug.Log("[WebUiToolchainBootstrap] node_modules not found; running pnpm install...");
            var nodeDirectory = Path.GetDirectoryName(WebUiPaths.NodeBinary);
            var exitCode = EditorProcessRunner.Run(WebUiPaths.PnpmBinary, "install --frozen-lockfile", WebUiPaths.WebuiRoot, nodeDirectory);
            if (exitCode != 0)
            {
                throw new BuildFailedException($"Web UI pnpm install failed with exit code {exitCode}");
            }
        }
    }
}
#endif
