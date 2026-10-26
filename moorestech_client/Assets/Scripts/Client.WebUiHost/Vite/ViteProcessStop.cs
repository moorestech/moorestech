using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    internal static class ViteProcessStop
    {
        internal const int ExitWaitMilliseconds = 2000;

        internal static void Terminate(Process process)
        {
            if (process.HasExited) return;
            var pid = process.Id;
            string childFailure = null;
            // 子停止コマンドという外部プロセス境界を隔離し、本人の停止を続ける
            // Isolate the child-stop external process boundary and still stop the owned parent
            try
            {
                var exitCode = ViteProcessKiller.KillProcessTree(pid);
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                if (exitCode == 1) Debug.Log($"[WebUiHost] no child processes for owned Vite pid={pid}");
                else if (exitCode != 0) childFailure = $"child stop command exit={exitCode}";
#else
                if (exitCode != 0 && !process.HasExited) childFailure = $"tree stop command exit={exitCode}";
#endif
            }
            catch (Exception error)
            {
                childFailure = error.GetBaseException().Message;
            }
            if (childFailure != null) Debug.LogError($"[WebUiHost] child shutdown unconfirmed for {pid}: {childFailure}");

            // 子なしprocessも本人を終了させ、Dispose前に実終了を確認する
            // Terminate even a childless owner and confirm exit before disposing its handle
            if (!process.HasExited) process.Kill();
            if (!process.WaitForExit(ExitWaitMilliseconds))
                throw new TimeoutException($"Owned Vite process {pid} did not exit within {ExitWaitMilliseconds}ms");
            if (childFailure != null)
                throw new InvalidOperationException($"Owned Vite {pid} exited but child shutdown is unconfirmed: {childFailure}");
        }
    }
}
