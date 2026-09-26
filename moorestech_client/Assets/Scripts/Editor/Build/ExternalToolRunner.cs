using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Client.Editor.Build
{
    /// <summary>
    /// ビルド後処理で使うOSコマンドを実行し終了コードを返す
    /// Runs OS commands used after the build and returns the exit code
    /// </summary>
    internal static class ExternalToolRunner
    {
        // 外部プロセス境界: 権限付与と署名は.NET Standard 2.1にAPIが無いためOSのコマンドへ委譲する
        // External process boundary: .NET Standard 2.1 has no permission or signing API, so delegate to OS commands
        public static int Run(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, RedirectStandardError = true };
            var process = Process.Start(startInfo);
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 失敗理由は呼び出し側の判断より先にそのまま残す
            // Keep the raw failure reason before the caller decides what to do
            if (process.ExitCode != 0) Debug.LogWarning($"[ExternalToolRunner] {fileName} {arguments} exited {process.ExitCode}: {standardError}");
            return process.ExitCode;
        }
    }
}
