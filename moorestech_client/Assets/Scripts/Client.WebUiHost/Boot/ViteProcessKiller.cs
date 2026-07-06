using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Boot
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
