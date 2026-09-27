#if UNITY_EDITOR
using System;
using System.IO;
using Client.RemoteExec.Loading;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build.Bundlers
{
    internal static class RemoteExecHarmonyBundler
    {
        internal static void Bundle(BuildTarget target, string outputPath, bool isStrict)
        {
            var source = Path.Combine(Application.dataPath, "Packages", "Lib.Harmony.2.4.2", "lib", "net48", RemoteExecHarmonyLoader.BundledFileName);
            if (!File.Exists(source) || CefLfsPointer.IsPointerFile(source))
            {
                Fail($"Harmony DLL is missing or an LFS pointer: {source}");
                return;
            }

            // Application.dataPathと一致する場所へプラグイン以外として同梱する
            // Ship outside the managed plugins under the player's Application.dataPath
            var dataPath = target == BuildTarget.StandaloneOSX
                ? Path.Combine(outputPath, "Contents")
                : WindowsPlayerPluginsDirectory.ResolveDataDirectory(outputPath);
            var destination = Path.Combine(dataPath, RemoteExecHarmonyLoader.BundledRelativeDirectory);

            // 配布ファイルへのIO失敗は、欠損したビルドとして明示的に失敗させる
            // Fail the build explicitly when output disk IO leaves Harmony missing
            try
            {
                Directory.CreateDirectory(destination);
                File.Copy(source, Path.Combine(destination, RemoteExecHarmonyLoader.BundledFileName), true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Fail($"Harmony copy failed: {e.Message}");
                return;
            }
            Debug.Log($"[RemoteExecHarmonyBundler] bundled at {destination}");

            #region Internal

            void Fail(string message)
            {
                // strict時はHarmony無しの成果物を出さないため即失敗、CI互換時は他Bundlerと同じく警告のみ
                // Strict mode refuses to ship an artifact without Harmony; CI-compatible mode only warns, matching the other bundlers
                if (isStrict) throw new BuildFailedException("[RemoteExecHarmonyBundler] " + message);
                Debug.LogWarning("[RemoteExecHarmonyBundler] " + message);
            }

            #endregion
        }
    }
}
#endif
