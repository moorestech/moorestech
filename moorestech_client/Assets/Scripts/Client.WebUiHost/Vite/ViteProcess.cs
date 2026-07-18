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

        // 確定実ポート。ready前は0
        // Actual port parsed from the stdout Local line; 0 before ready
        public int ActualPort => _actualPort;
        private volatile int _actualPort;

        public async UniTask<bool> StartAsync(int kestrelPort, int requestedPort)
        {
            var nodePath = WebUiPaths.NodeBinary;
            var pnpmPath = WebUiPaths.PnpmBinary;
            var webuiRoot = WebUiPaths.WebuiRoot;

            // node/pnpm/webuiRoot が欠けていれば起動不可として false を返す（呼び出し元がホスト無効化に使う）
            // Return false when node/pnpm/webuiRoot is missing, marking startup unavailable (caller disables the host)
            if (!IsEnvironmentReady()) return false;

            // クラッシュした過去セッションの孤児 Vite を spawn 前に掃除する（自 worktree 分のみ）
            // Sweep orphaned Vite processes from crashed sessions before spawning (this worktree only)
            SweepOrphanVitesBeforeSpawn();

            // 過去の tsc -b が生成した vite.config.js は vite.config.ts より優先ロードされ旧ポート設定を焼き込むため、spawn 前に削除する
            // Stale vite.config.js emitted by past tsc -b shadows vite.config.ts with baked-in old ports; delete it before spawning
            DeleteStaleViteConfigArtifacts();

            // node_modules が無ければ pnpm install を先に走らせる
            // Run pnpm install first if node_modules is missing
            await PnpmInstaller.RunIfNeeded(nodePath, pnpmPath, webuiRoot);

            _readySignal = new ManualResetEventSlim(false);
            _process = SpawnViteDev();

            // stdout の Local 行（実ポート確定）が出るまで待機（最大 30 秒）。時間内に来なければ false
            // Wait for the stdout Local line (actual port resolved), capped at 30 seconds; false when it does not arrive
            var ready = await WaitForReady(30);

            // 確定した (pid, port) を SessionState へ記録し、クリーンアップ時の照合 kill に使う
            // Record the resolved (pid, port) in SessionState for verified kill during cleanup
            RecordSpawnedForCleanup(ready);
            return ready;

            #region Internal

            void SweepOrphanVitesBeforeSpawn()
            {
#if UNITY_EDITOR
                ViteProcessKiller.KillOrphansOfThisWorkspace(webuiRoot);
#endif
            }

            void RecordSpawnedForCleanup(bool isReady)
            {
#if UNITY_EDITOR
                if (isReady) ViteProcessKiller.RecordSpawned(_process.Id, _actualPort, webuiRoot);
#endif
            }

            void DeleteStaleViteConfigArtifacts()
            {
                // どちらも .gitignore 登録済みの純生成物（tsconfig.node.json は emitDeclarationOnly 化済みで今後は生成されない）
                // Both are pure build artifacts already in .gitignore (tsconfig.node.json now uses emitDeclarationOnly, so no new ones)
                foreach (var name in new[] { "vite.config.js", "vite.config.d.ts" })
                {
                    var stale = Path.Combine(webuiRoot, name);
                    if (!File.Exists(stale)) continue;
                    File.Delete(stale);
                    Debug.Log($"[WebUiHost] deleted stale config artifact: {name}");
                }
            }

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

            Process SpawnViteDev()
            {
                // pnpm を直接 FileName に指定。pnpm exec が webui/node_modules 内の vite を起動する
                // Use pnpm directly as FileName; pnpm exec finds vite inside webui/node_modules
                var psi = new ProcessStartInfo
                {
                    FileName = pnpmPath,
                    // strictPort を付けない: 占有時は Vite が自動で次のポートへインクリメントする
                    // No strictPort: Vite auto-increments to the next port when the base is occupied
                    Arguments = $"exec vite --port {requestedPort} --host 127.0.0.1",
                    WorkingDirectory = webuiRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                var nodeBinDir = Path.GetDirectoryName(nodePath);
                psi.Environment["PATH"] = $"{nodeBinDir}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
                // 実ポートをvite proxy先へ注入
                // Inject the actual Kestrel port into the vite.config.ts proxy target
                psi.Environment["MOORESTECH_BACKEND_PORT"] = kestrelPort.ToString();
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

                // 実ポート確定時点でready
                // Ready once the actual port is resolved from the Local line
                if (ViteOutputParser.TryParseLocalPort(e.Data, out var port))
                {
                    _actualPort = port;
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
