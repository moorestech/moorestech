using System.IO;
using Client.Editor.Build.Bundlers;
using Client.ExternalProcess;
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

        // 署名できたかを返し、成果物の扱いは呼び出し側（BuildPipeline）が決める
        // Returns whether signing succeeded; BuildPipeline decides what happens to the artifact
        public static bool Sign(string appPath)
        {
            // 入れ子のコードを先に署名してから.app全体を封印する
            // Sign nested code first, then seal the whole .app
            var bundledFfmpeg = MacPlayerAppBundle.ResolveBundledFfmpegPath(appPath);
            if (!File.Exists(bundledFfmpeg))
            {
                // 個別署名を飛ばしても後続の--deepが検査するため続行するが、無音にはしない
                // Skipping the per-file sign still lets the later --deep pass verify it, but this must not stay silent
                Debug.LogWarning($"[MacAppAdHocSigner] bundled ffmpeg not found, skipping its individual signing: {bundledFfmpeg}");
            }
            else if (Codesign($"--force -s - \"{bundledFfmpeg}\"") != 0)
            {
                return Fail($"codesign failed for bundled ffmpeg: {bundledFfmpeg}");
            }
            if (Codesign($"--force --deep -s - \"{appPath}\"") != 0)
            {
                return Fail($"codesign failed for app: {appPath}");
            }

            // 署名が実際に通るかを配布前に確かめる
            // Confirm before distribution that the signature actually verifies
            if (Codesign($"--verify --deep --strict \"{appPath}\"") != 0)
            {
                return Fail($"codesign verification failed: {appPath}");
            }
            Debug.Log($"[MacAppAdHocSigner] ad-hoc signed and verified: {appPath}");
            return true;

            #region Internal

            int Codesign(string arguments) => EditorProcessRunner.Run(CodesignPath, arguments, Application.dataPath, "");

            bool Fail(string message)
            {
                Debug.LogError("[MacAppAdHocSigner] " + message);
                return false;
            }

            #endregion
        }
    }
}
