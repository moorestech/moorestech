using System;
using System.Diagnostics;
using System.IO;
using Client.WebUiHost.Common;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Vite/Node のネイティブプロセスを OS コマンドで停止する（プロセスツリー kill・ポート掃除）
    /// Stops Vite/Node native processes via OS commands (process-tree kill, port sweep)
    /// </summary>
    public static class ViteProcessKiller
    {
        // 親 pid 直下の子プロセス（pnpm 経由の node）を kill
        // Kill direct children of the parent pid (node spawned via pnpm)
        public static void KillProcessTree(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            RunDetached("/usr/bin/pkill", $"-P {pid}");
#elif UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /T /PID {pid}");
#endif
        }

        // 外部コマンドを fire-and-forget で実行。戻り値・例外は無視
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
        // SessionState キー。ドメインリロードを跨いで自インスタンスの Vite (pid, port) を追跡する
        // SessionState keys tracking this instance's Vite (pid, port) across domain reloads
        private const string SessionKeyVitePid = "WebUiHost.VitePid";
        private const string SessionKeyVitePort = "WebUiHost.VitePort";

        public static void RecordSpawned(int pid, int port)
        {
            UnityEditor.SessionState.SetInt(SessionKeyVitePid, pid);
            UnityEditor.SessionState.SetInt(SessionKeyVitePort, port);
        }

        // 自インスタンスが記録した Vite を掃除する。pid 再利用誤爆を防ぐため「記録ポートを当該 pid が今も LISTEN しているか」を照合する
        // Sweep the Vite this instance recorded; verify the pid still listens on the recorded port to avoid pid-reuse misfire
        public static void KillAnyLingering()
        {
            var pid = UnityEditor.SessionState.GetInt(SessionKeyVitePid, 0);
            var port = UnityEditor.SessionState.GetInt(SessionKeyVitePort, 0);
            if (pid == 0 || port == 0) return;

            if (FindPidOnPort(port) == pid)
            {
                KillPid(pid);
            }
            UnityEditor.SessionState.EraseInt(SessionKeyVitePid);
            UnityEditor.SessionState.EraseInt(SessionKeyVitePort);
        }

        // クラッシュした過去セッションの孤児 Vite を掃除する。cwd が自 worktree の webuiRoot に一致するものだけを対象にし、他 worktree の Vite には触れない
        // Sweep orphaned Vite processes from crashed sessions; only those whose cwd matches this worktree's webuiRoot, never touching other worktrees
        public static void KillOrphansOfThisWorkspace(string webuiRoot)
        {
            var normalizedRoot = Path.GetFullPath(webuiRoot).TrimEnd(Path.DirectorySeparatorChar);
            for (var port = WebUiPortConfig.ViteBasePort; port < WebUiPortConfig.ViteBasePort + WebUiPortConfig.PortSearchRange; port++)
            {
                var pid = FindPidOnPort(port);
                if (pid == 0) continue;
                if (GetProcessCwd(pid) != normalizedRoot) continue;

                Debug.Log($"[WebUiHost] killing orphaned vite (pid={pid}, port={port})");
                KillPid(pid);
            }
        }

        private static void KillPid(int pid)
        {
#if UNITY_EDITOR_WIN
            RunDetached(@"C:\Windows\System32\taskkill.exe", $"/F /PID {pid}");
#else
            RunDetached("/bin/kill", $"-9 {pid}");
#endif
        }

        // 指定ポートを listen している pid を返す。見つからなければ 0
        // Return the pid listening on the given port; 0 if not found
        private static int FindPidOnPort(int port)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            var output = RunAndCapture(LsofPath(), $"-ti :{port} -sTCP:LISTEN");
            var firstLine = output.Trim().Split('\n')[0].Trim();
            return int.TryParse(firstLine, out var pid) ? pid : 0;
#else
            // Windows: TODO netstat ベースの pid 特定（現状は未対応）
            // Windows: TODO netstat-based pid lookup (not yet implemented)
            return 0;
#endif
        }

        // 指定 pid のカレントディレクトリを返す。取得できなければ null
        // Return the cwd of the given pid; null when unavailable
        private static string GetProcessCwd(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            // lsof -Fn の出力から cwd 行（n で始まる行）を取り出す
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

        // 外部コマンドを実行して stdout を回収する。コマンド欠如時は空文字
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
