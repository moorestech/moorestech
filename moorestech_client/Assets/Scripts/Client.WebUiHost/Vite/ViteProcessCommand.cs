using System;
using System.Diagnostics;
using System.IO;

namespace Client.WebUiHost.Vite
{
    internal static class ViteProcessCommand
    {
        private const int CommandWaitMilliseconds = 1500;

        internal static int Run(string fileName, string arguments)
        {
            if (!File.Exists(fileName)) throw new FileNotFoundException("Process command is missing", fileName);
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var command = Process.Start(startInfo);
            if (command == null) throw new InvalidOperationException($"Could not start {fileName}");
            UnityEngine.Debug.Log($"[WebUiHost] owned helper pid={command.Id}, command={fileName} {arguments}");
            // 自分の補助コマンドだけを上限内で待ち、未終了なら本人だけを停止する
            // Bound the wait for our helper command and kill only that command on timeout
            if (!command.WaitForExit(CommandWaitMilliseconds))
            {
                var pid = command.Id;
                command.Kill();
                if (!command.WaitForExit(ViteProcessStop.ExitWaitMilliseconds))
                    throw new TimeoutException($"Owned helper {pid} remains after forced termination");
                throw new TimeoutException($"Process command timed out: {fileName} {arguments}");
            }
            return command.ExitCode;
        }
    }
}
