using System;
using System.Diagnostics;
using System.IO;
using Client.WebUiHost.Common;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Vite/NodeをOSコマンドで停止(kill/ポート掃除)
    /// Stops Vite/Node native processes via OS commands (process-tree kill, port sweep)
    /// </summary>
    public static class ViteProcessKiller
    {
        // 親pid直下の子(pnpm経由node)をkill
        // Kill direct children of the parent pid (node spawned via pnpm)
        public static void KillProcessTree(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            RunDetached("/usr/bin/pkill", $"-P {pid}");
#elif UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /T /PID {pid}");
#endif
        }

        // 外部コマンドをfire-and-forgetで実行
        // Run an external command fire-and-forget; return value and exit status are ignored
        public static void RunDetached(string fileName, string arguments)
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
        // SessionState キー。ドメインリロードを跨いで自インスタンスの Vite (pid, port, webuiRoot) を追跡する
        // SessionState keys tracking this instance's Vite (pid, port, webuiRoot) across domain reloads
        private const string SessionKeyVitePid = "WebUiHost.VitePid";
        private const string SessionKeyVitePort = "WebUiHost.VitePort";
        private const string SessionKeyViteRoot = "WebUiHost.ViteWebuiRoot";

        public static void RecordSpawned(int spawnedPid, int port, string webuiRoot)
        {
            // pnpm 親 pid ではなく、実際にポートを LISTEN している node 子 pid を優先して記録する
            // Prefer the node child pid actually listening on the port over the spawned pnpm parent pid
            var listenPid = FindPidOnPort(port);
            UnityEditor.SessionState.SetInt(SessionKeyVitePid, listenPid != 0 ? listenPid : spawnedPid);
            UnityEditor.SessionState.SetInt(SessionKeyVitePort, port);
            UnityEditor.SessionState.SetString(SessionKeyViteRoot, NormalizeRoot(webuiRoot));
        }

        // 自インスタンスが記録した Vite を掃除する。pid 再利用誤爆を防ぐため「記録ポートの LISTEN」と「cwd の一致」を照合する
        // Sweep the Vite this instance recorded; verify both the recorded-port LISTEN and the cwd match to avoid pid-reuse misfire
        public static void KillAnyLingering()
        {
            var pid = UnityEditor.SessionState.GetInt(SessionKeyVitePid, 0);
            var port = UnityEditor.SessionState.GetInt(SessionKeyVitePort, 0);
            var root = UnityEditor.SessionState.GetString(SessionKeyViteRoot, "");
            if (pid == 0 || port == 0) return;

            if (FindPidOnPort(port) == pid && (root == "" || GetProcessCwd(pid) == root))
            {
                KillPid(pid);
            }
            UnityEditor.SessionState.EraseInt(SessionKeyVitePid);
            UnityEditor.SessionState.EraseInt(SessionKeyVitePort);
            UnityEditor.SessionState.EraseString(SessionKeyViteRoot);
        }

        // クラッシュした過去セッションの孤児 Vite を掃除する。cwd が自 worktree の webuiRoot に一致するものだけを対象にし、他 worktree の Vite には触れない
        // Sweep orphaned Vite processes from crashed sessions; only those whose cwd matches this worktree's webuiRoot, never touching other worktrees
        public static void KillOrphansOfThisWorkspace(string webuiRoot)
        {
            var normalizedRoot = NormalizeRoot(webuiRoot);

            foreach (var pid in FindPidsInPortRange(WebUiPortConfig.ViteBasePort, WebUiPortConfig.ViteBasePort + WebUiPortConfig.PortSearchRange - 1))
            {
                if (GetProcessCwd(pid) != normalizedRoot) continue;

                Debug.Log($"[WebUiHost] killing orphaned vite (pid={pid})");
                KillPid(pid);
            }
        }

        // 指定ポート範囲を LISTEN している pid を列挙する。lsof の逐次 spawn による起動遅延を避けるためレンジ指定 1 回で取る
        // Enumerate pids listening within the port range; a single lsof range query avoids per-port spawn startup latency
        private static int[] FindPidsInPortRange(int firstPort, int lastPort)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            var output = RunAndCapture(LsofPath(), $"-ti :{firstPort}-{lastPort} -sTCP:LISTEN");
            var pids = new System.Collections.Generic.List<int>();
            foreach (var line in output.Split('\n'))
            {
                if (int.TryParse(line.Trim(), out var pid) && pid != 0) pids.Add(pid);
            }
            return pids.ToArray();
#else
            // Windows: TODO netstat実装
            // Windows: TODO netstat-based pid lookup (not yet implemented)
            return Array.Empty<int>();
#endif
        }

        private static string NormalizeRoot(string webuiRoot)
        {
            return Path.GetFullPath(webuiRoot).TrimEnd(Path.DirectorySeparatorChar);
        }

        private static void KillPid(int pid)
        {
#if UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /PID {pid}");
#else
            RunDetached("/bin/kill", $"-9 {pid}");
#endif
        }

        // listenするpidを返す。無ければ0
        // Return the pid listening on the given port; 0 if not found
        private static int FindPidOnPort(int port)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            var output = RunAndCapture(LsofPath(), $"-ti :{port} -sTCP:LISTEN");
            var firstLine = output.Trim().Split('\n')[0].Trim();
            return int.TryParse(firstLine, out var pid) ? pid : 0;
#else
            // Windows: TODO netstat実装
            // Windows: TODO netstat-based pid lookup (not yet implemented)
            return 0;
#endif
        }

        // pidのcwdを返す。取得不可はnull
        // Return the cwd of the given pid; null when unavailable
        private static string GetProcessCwd(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            // lsof -Fnのcwd行(n始まり)を抽出
            // Extract the cwd line (prefixed with 'n') from lsof -Fn output
            var output = RunAndCapture(LsofPath(), $"-a -p {pid} -d cwd -Fn");
            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("n", StringComparison.Ordinal))
                    return line.Substring(1).Trim().TrimEnd(Path.DirectorySeparatorChar);
            }
            return null;
#else
            return null;
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        private static string LsofPath()
        {
            return File.Exists("/usr/sbin/lsof") ? "/usr/sbin/lsof"
                 : File.Exists("/usr/bin/lsof") ? "/usr/bin/lsof"
                 : null;
        }

        // コマンド実行しstdout回収。無ければ空文字
        // Run an external command and capture stdout; empty string when the command is missing
        private static string RunAndCapture(string fileName, string arguments)
        {
            if (fileName == null || !File.Exists(fileName)) return "";
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
            if (p == null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            p.Dispose();
            return output;
        }
#endif
#endif
    }
}
