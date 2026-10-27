#if UNITY_EDITOR
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// 記録済み・孤児の Vite pid を OS コマンドで止める
    /// Stops recorded or orphaned Vite pids via OS commands
    /// </summary>
    internal static class VitePidTerminator
    {
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
    }
}
#endif
