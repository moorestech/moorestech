using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Node を spawn して Vite dev server を起動し、終了時に kill する
    /// Spawn Node to run Vite dev server; kill it on shutdown
    /// </summary>
    public class ViteProcess
    {
        private Process _process;
        private readonly ManualResetEventSlim _readySignal = new(false);

        public async UniTask StartAsync()
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            // Node / pnpm バイナリ / webui ディレクトリの存在確認
            // Verify node / pnpm binaries and webui dir are present
            if (!File.Exists(nodePath))
            {
                Debug.LogError($"[WebUiHost] Node binary not found at {nodePath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                return;
            }
            if (!File.Exists(pnpmPath))
            {
                Debug.LogError($"[WebUiHost] pnpm binary not found at {pnpmPath}. Run moorestech_web/setup.sh (or setup.ps1) first.");
                return;
            }
            if (!Directory.Exists(webuiRoot))
            {
                Debug.LogError($"[WebUiHost] webui dir not found at {webuiRoot}.");
                return;
            }

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            if (!Directory.Exists(Path.Combine(webuiRoot, "node_modules")))
            {
                await RunPnpmInstallAsync(nodePath, pnpmPath, webuiRoot);
            }

            // Vite dev server を spawn
            // Spawn Vite dev server
            _process = SpawnViteDev(nodePath, pnpmPath, webuiRoot);

            // stdout に "ready in" が出るまで待機（最大 30 秒）
            // Wait for "ready in" marker in stdout (cap 30 seconds)
            await WaitForReadyAsync(30);
        }

        public void Kill()
        {
            if (_process == null) return;
            if (_process.HasExited) { _process = null; return; }

            // Unix ではプロセスグループごと kill して孫プロセス(node)まで確実に終了させる
            // On Unix, kill the entire process group to ensure grandchild node processes also terminate
            KillProcessTree(_process);

            _process.WaitForExit(2000);
            _process.Dispose();
            _process = null;
            Debug.Log("[WebUiHost] Vite process killed");
        }

        private static void KillProcessTree(Process root)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            // macOS / Linux: 子孫プロセスを pgrep で列挙して全て kill
            // macOS/Linux: enumerate descendant pids with pgrep then kill each
            try
            {
                var ppid = root.Id;
                // pgrep -P <ppid> で直接の子プロセスを取得
                // Use pgrep -P to get direct children of the root process
                var pgrepPsi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/pgrep",
                    Arguments = $"-P {ppid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var pgrepProc = Process.Start(pgrepPsi);
                var childPidsStr = pgrepProc?.StandardOutput.ReadToEnd() ?? "";
                pgrepProc?.WaitForExit(1000);

                // 子プロセスを先に kill してからルートを kill
                // Kill children first, then the root
                foreach (var line in childPidsStr.Split('\n'))
                {
                    if (int.TryParse(line.Trim(), out var childPid))
                    {
                        var killPsi = new ProcessStartInfo
                        {
                            FileName = "/bin/kill",
                            Arguments = $"-TERM {childPid}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        };
                        using var killProc = Process.Start(killPsi);
                        killProc?.WaitForExit(500);
                    }
                }
                root.Kill();
            }
            catch
            {
                // フォールバック: 直接 Kill
                // Fallback: direct Kill
                root.Kill();
            }
#else
            // Windows: Kill() はプロセスツリーを走査する必要があるが、今はシンプルに直接 Kill
            // Windows: would need to enumerate children; for now just kill root
            root.Kill();
#endif
        }

        private async UniTask RunPnpmInstallAsync(string nodePath, string pnpmPath, string cwd)
        {
            Debug.Log("[WebUiHost] running pnpm install...");
            // pnpm はネイティブバイナリなので直接 FileName に指定し、node bin を PATH に追加
            // pnpm is a native binary; set it as FileName and prepend node bin dir to PATH
            var psi = new ProcessStartInfo
            {
                FileName = pnpmPath,
                Arguments = "install",
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var nodeBinDir = Path.GetDirectoryName(nodePath);
            psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{System.Environment.GetEnvironmentVariable("PATH")}";
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

        private Process SpawnViteDev(string nodePath, string pnpmPath, string cwd)
        {
            // pnpm を直接 FileName に指定。pnpm exec が webui/node_modules 内の vite を起動する
            // Use pnpm directly as FileName; pnpm exec finds vite inside webui/node_modules
            var psi = new ProcessStartInfo
            {
                FileName = pnpmPath,
                Arguments = "exec vite --port 5173 --strictPort --host 127.0.0.1",
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // バンドル済み node を PATH 先頭に追加して pnpm が正しい node を使うよう保証
            // Prepend bundled node bin dir to PATH so pnpm uses our bundled node
            var nodeBinDir = Path.GetDirectoryName(nodePath);
            psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{System.Environment.GetEnvironmentVariable("PATH")}";
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

        private void OnViteStdout(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            Debug.Log($"[Vite] {e.Data}");
            if (e.Data.Contains("ready in") || e.Data.Contains("Local:"))
            {
                _readySignal.Set();
            }
        }

        private async UniTask WaitForReadyAsync(int timeoutSec)
        {
            var start = DateTime.UtcNow;
            while (!_readySignal.IsSet)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > timeoutSec)
                {
                    Debug.LogError($"[WebUiHost] Vite did not become ready within {timeoutSec}s");
                    return;
                }
                await UniTask.Delay(100);
            }
        }
    }
}
