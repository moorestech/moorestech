using System.IO;
using Client.Game.InGame.BugReport.Recording;
using Client.WebUiHost.Editor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// 同梱を終えた.appをad-hoc署名し直す。ビルド後に入れたCEF helperとffmpegでUnityの署名の封印が崩れるため（ADR 0071）
    /// Re-signs the bundled .app ad-hoc, because the CEF helper and ffmpeg added after the build break Unity's signature seal (ADR 0071)
    /// </summary>
    internal static class MacAppAdHocSigner
    {
        private const string CodesignPath = "/usr/bin/codesign";

        public static void Sign(string appPath, bool isStrict)
        {
            // 入れ子のコードを先に署名してから.app全体を封印する
            // Sign nested code first, then seal the whole .app
            var bundledFfmpeg = FfmpegLocator.ResolveBundledMacExecutablePath(appPath);
            if (!File.Exists(bundledFfmpeg))
            {
                // 個別署名を飛ばしても後続の--deepが検査するため続行するが、無音にはしない
                // Skipping the per-file sign still lets the later --deep pass verify it, but this must not stay silent
                Debug.LogWarning($"[MacAppAdHocSigner] bundled ffmpeg not found, skipping its individual signing: {bundledFfmpeg}");
            }
            else if (EditorProcessRunner.Run(CodesignPath, $"--force -s - \"{bundledFfmpeg}\"", Application.dataPath, "") != 0)
            {
                Fail($"codesign failed for bundled ffmpeg: {bundledFfmpeg}");
            }
            if (EditorProcessRunner.Run(CodesignPath, $"--force --deep -s - \"{appPath}\"", Application.dataPath, "") != 0)
            {
                Fail($"codesign failed for app: {appPath}");
                return;
            }

            // 署名が実際に通るかを配布前に確かめる
            // Confirm before distribution that the signature actually verifies
            if (EditorProcessRunner.Run(CodesignPath, $"--verify --deep --strict \"{appPath}\"", Application.dataPath, "") != 0)
            {
                Fail($"codesign verification failed: {appPath}");
                return;
            }
            Debug.Log($"[MacAppAdHocSigner] ad-hoc signed and verified: {appPath}");

            #region Internal

            void Fail(string message)
            {
                if (isStrict) throw new BuildFailedException("[MacAppAdHocSigner] " + message);
                Debug.LogWarning("[MacAppAdHocSigner] " + message);
            }

            #endregion
        }
    }
}
