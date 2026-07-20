using System;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// node_modules 無ければ pnpm install 実行
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

            // リダイレクトした両ストリームを排水しながら待つ（読まずに WaitForExit するとパイプ 64KB 超で子プロセスが write ブロックしハングする）
            // Drain both redirected streams while waiting (WaitForExit without reading deadlocks once the child fills the 64KB pipe)
            var stderr = new System.Text.StringBuilder();
            p.OutputDataReceived += (_, _) => { };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) stderr.AppendLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            await UniTask.RunOnThreadPool(() => p.WaitForExit());
            if (p.ExitCode != 0)
            {
                Debug.LogError($"[WebUiHost] pnpm install exited with code {p.ExitCode}\n{stderr}");
            }
            else
            {
                Debug.Log("[WebUiHost] pnpm install complete");
            }
        }
    }
}
