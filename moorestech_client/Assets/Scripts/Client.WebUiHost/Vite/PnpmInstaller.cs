using System;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// node_modules が無い場合に pnpm install を実行する
    /// Runs pnpm install when node_modules is missing
    /// </summary>
    public static class PnpmInstaller
    {
        public static async UniTask RunIfNeeded(string nodePath, string pnpmPath, string webuiRoot)
        {
            if (Directory.Exists(Path.Combine(webuiRoot, "node_modules"))) return;

            Debug.Log("[WebUiHost] running pnpm install...");
            // pnpm はネイティブバイナリなので直接 FileName に指定し、node bin を PATH に追加
            // pnpm is a native binary; set it as FileName and prepend node bin dir to PATH
            var psi = new ProcessStartInfo
            {
                FileName = pnpmPath,
                Arguments = "install",
                WorkingDirectory = webuiRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var nodeBinDir = Path.GetDirectoryName(nodePath);
            psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
            using var p = Process.Start(psi);
            if (p == null)
            {
                Debug.LogError("[WebUiHost] pnpm install: failed to spawn process");
                return;
            }
            await UniTask.RunOnThreadPool(() => p.WaitForExit());
            if (p.ExitCode != 0)
            {
                Debug.LogError($"[WebUiHost] pnpm install exited with code {p.ExitCode}\n{p.StandardError.ReadToEnd()}");
            }
            else
            {
                Debug.Log("[WebUiHost] pnpm install complete");
            }
        }
    }
}
