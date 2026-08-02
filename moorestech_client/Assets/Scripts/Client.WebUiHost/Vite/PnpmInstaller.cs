using System.Diagnostics;
using System.IO;
using System.Text;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// node_modules 無ければ pnpm install 実行
    /// Runs pnpm install when node_modules is missing
    /// dev起動もビルドも同じ実装を通し、依存解決ポリシーが経路ごとに割れないようにする
    /// Dev startup and the build share this implementation so dependency policy never diverges per path
    /// </summary>
    public static class PnpmInstaller
    {
        // プロセスを生成できなかったことを表す終了コード
        // Exit code representing that the process could not be created
        private const int SpawnFailureExitCode = -1;

        public static async UniTask RunIfNeeded(string nodePath, string pnpmPath, string webuiRoot)
        {
            await UniTask.RunOnThreadPool(() => { InstallIfNeeded(nodePath, pnpmPath, webuiRoot); });
        }

        /// <summary>
        /// pnpm install を同期実行して終了コードを返す（node_modulesが既にあれば何もせず0）
        /// Runs pnpm install synchronously and returns its exit code (0 when node_modules already exists)
        /// </summary>
        public static int InstallIfNeeded(string nodePath, string pnpmPath, string webuiRoot)
        {
            if (Directory.Exists(Path.Combine(webuiRoot, "node_modules"))) return 0;

            Debug.Log("[WebUiHost] running pnpm install...");

            // pnpm はネイティブバイナリなので直接 FileName に指定する
            // pnpm is a native binary, so it is set directly as FileName
            var startInfo = new ProcessStartInfo
            {
                FileName = pnpmPath,
                // lockfile固定で入れ、開発機とビルド機で依存を一致させる
                // Install pinned to the lockfile so dev machines and build machines resolve the same tree
                Arguments = "install --frozen-lockfile",
                WorkingDirectory = webuiRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 不正UTF-8のenvを除外してから node bin を PATH 先頭へ足す
            // Sanitize corrupted env entries, then prepend the node bin dir to PATH
            SanitizedProcessEnvironment.Sanitize(startInfo);
            SanitizedProcessEnvironment.PrependPath(startInfo, Path.GetDirectoryName(nodePath));

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Debug.LogError("[WebUiHost] pnpm install: failed to spawn process");
                return SpawnFailureExitCode;
            }

            // リダイレクトした両ストリームを排水しながら待つ（読まずに WaitForExit するとパイプ 64KB 超で子プロセスが write ブロックしハングする）
            // Drain both redirected streams while waiting (WaitForExit without reading deadlocks once the child fills the 64KB pipe)
            var errorText = new StringBuilder();
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) errorText.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0) Debug.LogError($"[WebUiHost] pnpm install exited with code {process.ExitCode}\n{errorText}");
            else Debug.Log("[WebUiHost] pnpm install complete");

            return process.ExitCode;
        }
    }
}
