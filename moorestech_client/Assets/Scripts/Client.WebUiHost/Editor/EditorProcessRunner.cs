#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Client.WebUiHost.Common;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Editor
{
    /// <summary>
    /// ビルド工程用の同期外部プロセス実行（env汚染除去・出力排水込み）
    /// Synchronous external-process runner for build steps, with env sanitizing and output draining
    /// </summary>
    public static class EditorProcessRunner
    {
        // 起動失敗（プロセスが生成できなかった）を表す終了コード
        // Exit code representing spawn failure (process could not be created)
        public const int SpawnFailureExitCode = -1;

        public static int Run(string fileName, string arguments, string workingDirectory, string prependPathDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 不正UTF-8のenvを除外してから、必要なPATH先頭追加を行う
            // Sanitize corrupted env entries first, then prepend the required PATH directory
            SanitizedProcessEnvironment.Sanitize(startInfo);
            if (!string.IsNullOrEmpty(prependPathDirectory))
            {
                startInfo.Environment["PATH"] = $"{prependPathDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
            }

            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして終了コードへ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into an exit code
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EditorProcessRunner] failed to start '{fileName} {arguments}': {exception.GetBaseException().Message}");
                return SpawnFailureExitCode;
            }

            if (process == null)
            {
                Debug.LogError($"[EditorProcessRunner] no process was created for '{fileName} {arguments}'");
                return SpawnFailureExitCode;
            }

            using (process)
            {
                // 両ストリームを排水しながら待つ（読まないとパイプ64KB超で子がwriteブロックしハングする）
                // Drain both streams while waiting (unread pipes block the child's writes past 64KB)
                var errorText = new StringBuilder();
                process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[{Path.GetFileName(fileName)}] {e.Data}"); };
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) errorText.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0 && errorText.Length > 0)
                {
                    Debug.LogError($"[EditorProcessRunner] '{fileName} {arguments}' exited with {process.ExitCode}\n{errorText}");
                }

                return process.ExitCode;
            }
        }
    }
}
#endif
