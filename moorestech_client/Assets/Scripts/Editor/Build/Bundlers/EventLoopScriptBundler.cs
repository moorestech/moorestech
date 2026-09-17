using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Editor.Build
{
    /// <summary>
    /// 展示会用の再起動ループスクリプトを成果物直下へ実行権つきで置く
    /// Places the exhibition restart-loop script at the artifact root with the executable bit set
    /// </summary>
    public static class EventLoopScriptBundler
    {
        private const string ScriptFileName = "start-gamescom-loop.command";

        public static void Bundle(string outputDirectory, bool isStrict)
        {
            // 正本はリポジトリの scripts/event
            // The source of truth is scripts/event in this repository
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "event", ScriptFileName));
            if (!File.Exists(sourcePath))
            {
                if (isStrict) throw new BuildFailedException($"[EventLoopScriptBundler] launch script is missing: {sourcePath}");
                Debug.LogWarning($"[EventLoopScriptBundler] launch script is missing: {sourcePath}");
                return;
            }

            var destinationPath = Path.Combine(outputDirectory, ScriptFileName);
            File.Copy(sourcePath, destinationPath, true);

            // コピー直後は実行権が落ちるため付け直す（ダブルクリック起動の前提）
            // The copy drops the executable bit, so restore it because the booth launches it by double-click
            MarkExecutable(destinationPath, isStrict);
            Debug.Log($"[EventLoopScriptBundler] bundled launch script: {destinationPath}");
        }

        private static void MarkExecutable(string filePath, bool isStrict)
        {
            // 外部プロセス境界: .NET Standard 2.1にパーミッション付与APIが無いためchmodへ委譲する
            // External process boundary: .NET Standard 2.1 has no permission API, so delegate to chmod
            var process = Process.Start(new ProcessStartInfo("/bin/chmod", $"+x \"{filePath}\"") { UseShellExecute = false });
            process.WaitForExit();
            if (process.ExitCode == 0) return;

            if (isStrict) throw new BuildFailedException($"[EventLoopScriptBundler] chmod failed: {filePath}");
            Debug.LogWarning($"[EventLoopScriptBundler] chmod failed: {filePath}");
        }
    }
}
