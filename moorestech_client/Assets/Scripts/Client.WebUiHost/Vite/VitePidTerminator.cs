using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Vite の pid を OS コマンドで止める（本人のツリー・記録済みや孤児の単体）。記録と孤児探しは VitePidTracker が持つ
    /// Stops Vite pids via OS commands (the owned tree, or a single recorded/orphaned pid); VitePidTracker records pids and finds orphans
    /// </summary>
    internal static class VitePidTerminator
    {
        // 子の停止コマンド結果を本人processの終了確認へ渡す
        // Return the child termination command result to the owner termination check
        internal static int KillProcessTree(int pid)
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            return ViteProcessCommand.Run("/usr/bin/pkill", $"-KILL -P {pid}");
#elif UNITY_EDITOR_WIN
            return ViteProcessCommand.Run(@"C:\Windows\System32\taskkill.exe", $"/F /T /PID {pid}");
#else
            throw new PlatformNotSupportedException("Vite process termination requires an Editor platform");
#endif
        }

#if UNITY_EDITOR
        // 停止コマンドが失敗しても対象が既に居なければ成功とみなす（先行終了は失敗でない）
        // A failed kill command still counts as success when the target is already gone (an earlier exit is not a failure)
        internal static bool TryKill(int pid, out string failure)
        {
#if UNITY_EDITOR_WIN
            var exitCode = ViteProcessCommand.Run(@"C:\Windows\System32\taskkill.exe", $"/F /PID {pid}");
#else
            var exitCode = ViteProcessCommand.Run("/bin/kill", $"-9 {pid}");
#endif
            failure = null;
            if (exitCode == 0) return true;
            if (!IsAlive(pid))
            {
                Debug.Log($"[WebUiHost] vite pid {pid} had already exited (cleanup command exit={exitCode})");
                return true;
            }
            failure = $"Vite cleanup command failed for {pid}: exit={exitCode}";
            return false;
        }

        private static bool IsAlive(int pid)
        {
            var alive = false;
            foreach (var process in Process.GetProcesses())
            {
                if (process.Id == pid) alive = true;
                process.Dispose();
            }
            return alive;
        }
#endif
    }
}
