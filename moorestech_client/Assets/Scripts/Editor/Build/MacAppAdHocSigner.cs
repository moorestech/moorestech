using System.IO;
using Client.Game.InGame.BugReport.Recording;
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
            var bundledFfmpeg = Path.Combine(appPath, "Contents", FfmpegLocator.BundledMacExecutableRelativePath);
            if (File.Exists(bundledFfmpeg) && ExternalToolRunner.Run(CodesignPath, $"--force -s - \"{bundledFfmpeg}\"") != 0)
            {
                Fail($"codesign failed for bundled ffmpeg: {bundledFfmpeg}");
                return;
            }
            if (ExternalToolRunner.Run(CodesignPath, $"--force --deep -s - \"{appPath}\"") != 0)
            {
                Fail($"codesign failed for app: {appPath}");
                return;
            }

            // 署名が実際に通るかを配布前に確かめる
            // Confirm before distribution that the signature actually verifies
            if (ExternalToolRunner.Run(CodesignPath, $"--verify --deep --strict \"{appPath}\"") != 0)
            {
                Fail($"codesign verification failed: {appPath}");
                return;
            }
            Debug.Log($"[MacAppAdHocSigner] ad-hoc signed and verified: {appPath}");

            #region Internal

            void Fail(string message)
            {
                // strict時は起動保証の無い成果物を配らない。CI互換時は警告のみ
                // Strict mode never ships an artifact without a launch guarantee; CI-compatible mode only warns
                if (isStrict) throw new BuildFailedException("[MacAppAdHocSigner] " + message);
                Debug.LogWarning("[MacAppAdHocSigner] " + message);
            }

            #endregion
        }
    }
}
