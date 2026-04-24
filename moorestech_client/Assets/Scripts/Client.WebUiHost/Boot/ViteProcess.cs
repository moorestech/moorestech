using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
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
        private ManualResetEventSlim _readySignal;

        public async UniTask StartAsync()
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            if (!IsEnvironmentReady()) return;

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            if (!Directory.Exists(Path.Combine(webuiRoot, "node_modules")))
            {
                await RunPnpmInstall();
            }

            _readySignal = new ManualResetEventSlim(false);
            _process = SpawnViteDev();

            // stdout に "ready in" が出るまで待機（最大 30 秒）
            // Wait for "ready in" marker in stdout (cap 30 seconds)
            await WaitForReady(30);

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

            async UniTask WaitForReady(int timeoutSec)
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

            #endregion
        }

        public void Kill()
        {
            if (_process == null) return;

            if (!_process.HasExited)
            {
                KillProcessTree(_process.Id);
                _process.WaitForExit(2000);
            }

            _process.Dispose();
            _process = null;
            _readySignal?.Dispose();
            _readySignal = null;
            Debug.Log("[WebUiHost] Vite process killed");

            #region Internal

            static void KillProcessTree(int pid)
            {
                // 親 pid 直下の子プロセス（pnpm 経由の node）を kill
                // Kill direct children of the parent pid (node spawned via pnpm)
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                RunDetached("/usr/bin/pkill", $"-P {pid}");
#elif UNITY_EDITOR_WIN
                RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /T /PID {pid}");
#endif
            }

            #endregion
        }

        // 外部コマンドを fire-and-forget で実行。戻り値・例外は無視
        // Run an external command fire-and-forget; return value and exit status are ignored
        private static void RunDetached(string fileName, string arguments)
        {
            if (!File.Exists(fileName)) return;
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var p = Process.Start(psi);
            if (p == null) return;
            p.WaitForExit(1500);
            p.Dispose();
        }

#if UNITY_EDITOR
        // インスタンス寿命を問わず、5173 を握っている Vite プロセスを掃除する
        // ポートを pid 特定経由で絞り込み、別 worktree の Editor で動いている Vite を巻き添えにしない
        // Sweep any Vite process bound to 5173, regardless of instance state.
        // Resolves the pid via port-binding lookup so other worktrees' Editors are not affected.
        public static void KillAnyLingering()
        {
            var pid = FindPidOnPort5173();
            if (pid == 0) return;
#if UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /PID {pid}");
#else
            RunDetached("/bin/kill", $"-9 {pid}");
#endif
        }

        // port 5173 を listen している pid を返す。見つからなければ 0
        // Return the pid listening on port 5173; 0 if not found
        private static int FindPidOnPort5173()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            var lsofPath = File.Exists("/usr/sbin/lsof") ? "/usr/sbin/lsof"
                         : File.Exists("/usr/bin/lsof") ? "/usr/bin/lsof"
                         : null;
            if (lsofPath == null) return 0;
            var psi = new ProcessStartInfo
            {
                FileName = lsofPath,
                Arguments = "-ti :5173 -sTCP:LISTEN",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var p = Process.Start(psi);
            if (p == null) return 0;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            p.Dispose();
            var firstLine = output.Trim().Split('\n')[0].Trim();
            return int.TryParse(firstLine, out var pid) ? pid : 0;
#else
            // Windows: TODO netstat ベースの pid 特定（現状は未対応）
            // Windows: TODO netstat-based pid lookup (not yet implemented)
            return 0;
#endif
        }
#endif
    }
}
