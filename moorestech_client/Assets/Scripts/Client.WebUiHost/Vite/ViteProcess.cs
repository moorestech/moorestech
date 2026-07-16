using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Node を spawn して Vite dev server を起動し、終了時に kill する
    /// Spawn Node to run Vite dev server; kill it on shutdown
    /// </summary>
    public class ViteProcess
    {
        private Process _process;
        private ManualResetEventSlim _readySignal;

        public async UniTask<bool> StartAsync()
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            // node/pnpm/webuiRoot が欠けていれば起動不可として false を返す（呼び出し元がホスト無効化に使う）
            // Return false when node/pnpm/webuiRoot is missing, marking startup unavailable (caller disables the host)
            if (!IsEnvironmentReady()) return false;

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            if (!Directory.Exists(Path.Combine(webuiRoot, "node_modules")))
            {
                await RunPnpmInstall();
            }

            _readySignal = new ManualResetEventSlim(false);
            _process = SpawnViteDev();

            // stdout に "ready in" が出るまで待機（最大 30 秒）。時間内に来なければ false
            // Wait for "ready in" marker in stdout (cap 30 seconds); false when it does not arrive in time
            return await WaitForReady(30);

            #region Internal

            bool IsEnvironmentReady()
            {
                if (!File.Exists(nodePath))
                {
                    Debug.LogError($"[WebUiHost] Node binary not found at {nodePath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                    return false;
                }
                if (!File.Exists(pnpmPath))
                {
                    Debug.LogError($"[WebUiHost] pnpm binary not found at {pnpmPath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                    return false;
                }
                if (!Directory.Exists(webuiRoot))
                {
                    Debug.LogError($"[WebUiHost] webui dir not found at {webuiRoot}.");
                    return false;
                }
                return true;
            }

            async UniTask RunPnpmInstall()
            {
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

            Process SpawnViteDev()
            {
                // pnpm を直接 FileName に指定。pnpm exec が webui/node_modules 内の vite を起動する
                // Use pnpm directly as FileName; pnpm exec finds vite inside webui/node_modules
                var psi = new ProcessStartInfo
                {
                    FileName = pnpmPath,
                    Arguments = "exec vite --port 5173 --strictPort --host 127.0.0.1",
                    WorkingDirectory = webuiRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                var nodeBinDir = Path.GetDirectoryName(nodePath);
                psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += OnViteStdout;
                p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[Vite] {e.Data}"); };
                p.Exited += (_, _) => Debug.Log("[WebUiHost] Vite process exited");
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                Debug.Log($"[WebUiHost] spawned Vite (pid={p.Id})");
                return p;
            }

            void OnViteStdout(object sender, DataReceivedEventArgs e)
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Debug.Log($"[Vite] {e.Data}");
                if (e.Data.Contains("ready in") || e.Data.Contains("Local:"))
                {
                    _readySignal.Set();
                }
            }

            async UniTask<bool> WaitForReady(int timeoutSec)
            {
                var start = DateTime.UtcNow;
                while (!_readySignal.IsSet)
                {
                    if ((DateTime.UtcNow - start).TotalSeconds > timeoutSec)
                    {
                        Debug.LogError($"[WebUiHost] Vite did not become ready within {timeoutSec}s");
                        return false;
                    }
                    await UniTask.Delay(100);
                }
                return true;
            }

            #endregion
        }

        public void Kill()
        {
            if (_process == null) return;

            if (!_process.HasExited)
            {
                ViteProcessKiller.KillProcessTree(_process.Id);
                _process.WaitForExit(2000);
            }

            _process.Dispose();
            _process = null;
            _readySignal?.Dispose();
            _readySignal = null;
            Debug.Log("[WebUiHost] Vite process killed");
        }
    }
}
