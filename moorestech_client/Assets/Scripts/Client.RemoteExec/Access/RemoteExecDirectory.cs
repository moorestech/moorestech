using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Game.Paths;
using Debug = UnityEngine.Debug;

namespace Client.RemoteExec.Access
{
    // access.json・台帳・実行印の置き場を作る唯一の口。トークンを含むため所有者限定の権限を設定する
    // The single mouth creating the directories for access.json, ledgers and signals; they hold a token, so access is restricted to the owner
    internal static class RemoteExecDirectory
    {
        private static readonly object Gate = new();
        private static readonly HashSet<string> Restricted = new();

        internal static string Path => GameSystemPaths.RemoteExecDirectory;

        internal static void EnsureCreated(string directory)
        {
            Directory.CreateDirectory(directory);
            lock (Gate)
            {
                if (!Restricted.Add(directory)) return;
            }
            Restrict(directory);
        }

        // 権限設定はOSのコマンドを起動する外部プロセス境界。失敗は握り潰さずログへ残す
        // Setting permissions launches an OS command at the external-process boundary; failures are logged rather than swallowed
        private static void Restrict(string directory)
        {
            var isWindows = System.IO.Path.DirectorySeparatorChar == '\\';
            var fileName = isWindows ? "icacls" : "/bin/chmod";
            var arguments = isWindows
                ? $"\"{directory}\" /inheritance:r /grant:r \"{Environment.UserName}\":(OI)(CI)F"
                : $"700 \"{directory}\"";
            try
            {
                var startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                using var process = Process.Start(startInfo);
                var error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0) return;
                Debug.LogError($"[RemoteExec] 置き場を所有者限定にできませんでした（トークンが同一マシンの他利用者から読めます） {directory}: {fileName} exit:{process.ExitCode} {error}");
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException)
            {
                Debug.LogError($"[RemoteExec] 権限設定コマンドを起動できませんでした（トークンが同一マシンの他利用者から読めます） {directory}: {e.Message}");
            }
        }
    }
}
