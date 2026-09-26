using System.IO;
using Client.WebUiHost.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// 展示会用の再起動ループスクリプトを成果物直下へ実行権つきで置く
    /// Places the exhibition restart-loop script at the artifact root with the executable bit set
    /// </summary>
    public static class EventLoopScriptBundler
    {
        private const string ScriptFileName = "start-gamescom-loop.command";

        public static void Bundle(BuildTarget target, string outputDirectory, bool isStrict)
        {
            // .commandはmacOS専用。用途を問わず他OSへ混ざると実行時にProcess.Startが落ちる
            // .command is macOS-only; mixing it into another OS build crashes Process.Start at run time regardless of purpose
            if (target != BuildTarget.StandaloneOSX)
            {
                Fail($"exhibition launch script is Mac-only, skipped for {target}");
                return;
            }

            // 正本はリポジトリの scripts/event
            // The source of truth is scripts/event in this repository
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "scripts", "event", ScriptFileName));
            if (!File.Exists(sourcePath))
            {
                Fail($"launch script is missing: {sourcePath}");
                return;
            }

            var destinationPath = Path.Combine(outputDirectory, ScriptFileName);
            File.Copy(sourcePath, destinationPath, true);

            // コピー直後は実行権が落ちるため付け直す（ダブルクリック起動の前提）
            // The copy drops the executable bit, so restore it because the booth launches it by double-click
            MarkExecutable(destinationPath);
            Debug.Log($"[EventLoopScriptBundler] bundled launch script: {destinationPath}");

            #region Internal

            void MarkExecutable(string filePath)
            {
                if (EditorProcessRunner.Run("/bin/chmod", $"+x \"{filePath}\"", Application.dataPath, "") == 0) return;

                Fail($"chmod failed: {filePath}");
            }

            void Fail(string message)
            {
                if (isStrict) throw new BuildFailedException("[EventLoopScriptBundler] " + message);
                Debug.LogWarning("[EventLoopScriptBundler] " + message);
            }

            #endregion
        }
    }
}
