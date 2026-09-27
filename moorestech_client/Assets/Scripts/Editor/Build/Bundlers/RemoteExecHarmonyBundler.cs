#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build.Bundlers
{
    internal static class RemoteExecHarmonyBundler
    {
        internal static void Bundle(BuildTarget target, string outputPath)
        {
            var source = Path.Combine(Application.dataPath, "Packages", "Lib.Harmony.2.4.2", "lib", "net48", "0Harmony.dll");
            if (!File.Exists(source) || CefLfsPointer.IsPointerFile(source))
            {
                Fail($"Harmony DLL is missing or an LFS pointer: {source}");
                return;
            }

            // Application.dataPathと一致する場所へプラグイン以外として同梱する
            // Ship outside the managed plugins under the player's Application.dataPath
            var dataPath = target == BuildTarget.StandaloneOSX
                ? Path.Combine(outputPath, "Contents")
                : Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "_Data");
            var destination = Path.Combine(dataPath, "RemoteExec");

            // 配布ファイルへのIO失敗は、欠損したビルドとして明示的に失敗させる
            // Fail the build explicitly when output disk IO leaves Harmony missing
            try
            {
                Directory.CreateDirectory(destination);
                File.Copy(source, Path.Combine(destination, "0Harmony.dll"), true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Fail($"Harmony copy failed: {e.Message}");
            }
            Debug.Log($"[RemoteExecHarmonyBundler] bundled at {destination}");
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"[RemoteExecHarmonyBundler] {reason}");
            throw new BuildFailedException(reason);
        }
    }
}
#endif
