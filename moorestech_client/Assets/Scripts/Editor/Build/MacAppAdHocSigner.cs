using System;
using System.IO;
using Client.Editor.Build.Bundlers;
using Client.ExternalProcess;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// 同梱を終えた.appをad-hoc署名し直す。ビルド後に入れたffmpegでUnityの署名の封印が崩れるため（ADR 0071）
    /// Re-signs the bundled .app ad-hoc, because the ffmpeg added after the build breaks Unity's signature seal (ADR 0071)
    /// MacホストはAppleのcodesign、それ以外のホストはrcodesign（apple-codesign。鍵なしでad-hoc署名する）を使う
    /// Mac hosts use Apple's codesign; other hosts use rcodesign (apple-codesign, which signs ad-hoc without a key)
    /// </summary>
    internal static class MacAppAdHocSigner
    {
        private const string CodesignPath = "/usr/bin/codesign";

        // Mac以外のホストで使うrcodesign.exeの場所。焼く側の環境が必ず渡す
        // Location of rcodesign.exe on non-Mac hosts; the baking environment must always provide it
        private const string RcodesignPathEnvKey = "MOORESTECH_RCODESIGN_PATH";

        // 署名できたかを返し、成果物の扱いは呼び出し側（BuildPipeline）が決める
        // Returns whether signing succeeded; BuildPipeline decides what happens to the artifact
        public static bool Sign(string appPath)
        {
            var bundledFfmpeg = MacPlayerAppBundle.ResolveBundledFfmpegPath(appPath);
            if (!File.Exists(bundledFfmpeg))
            {
                // 個別署名を飛ばしても後続の全体署名と検証が見るため続行するが、無音にはしない
                // Skipping the per-file sign still leaves the whole-app sign and verify to check it, but this must not stay silent
                Debug.LogWarning($"[MacAppAdHocSigner] bundled ffmpeg not found, skipping its individual signing: {bundledFfmpeg}");
            }

            var signed = Application.platform == RuntimePlatform.OSXEditor
                ? SignWithAppleCodesign(appPath, bundledFfmpeg)
                : SignWithRcodesign(appPath, bundledFfmpeg);
            if (signed) Debug.Log($"[MacAppAdHocSigner] ad-hoc signed: {appPath}");
            return signed;
        }

        private static bool SignWithAppleCodesign(string appPath, string bundledFfmpeg)
        {
            // 入れ子のコードを先に署名してから.app全体を封印する
            // Sign nested code first, then seal the whole .app
            if (File.Exists(bundledFfmpeg) && Codesign($"--force -s - \"{bundledFfmpeg}\"") != 0)
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
            return true;

            #region Internal

            int Codesign(string arguments) => EditorProcessRunner.Run(CodesignPath, arguments, Application.dataPath, "");

            #endregion
        }

        private static bool SignWithRcodesign(string appPath, string bundledFfmpeg)
        {
            // 必須の道具が無いまま焼き進めると未署名の.appが出るため、ここで止める
            // Going on without the required tool would ship an unsigned .app, so stop here
            var rcodesignPath = Environment.GetEnvironmentVariable(RcodesignPathEnvKey);
            if (string.IsNullOrWhiteSpace(rcodesignPath) || !File.Exists(rcodesignPath))
            {
                return Fail($"{RcodesignPathEnvKey} が未設定か、指す先にrcodesignがありません: '{rcodesignPath}'");
            }

            // rcodesignは.app内の入れ子のMach-Oも含めて署名する（codesignの--deep相当）
            // rcodesign signs nested Mach-O inside the .app as well (the equivalent of codesign --deep)
            if (Rcodesign($"sign \"{appPath}\"") != 0)
            {
                return Fail($"rcodesign failed for app: {appPath}");
            }

            // rcodesign verify はCMSの無いad-hoc署名を検証できないため、ここでは確かめない。配布前にMac側でcodesign --verify --deep --strictが必ず見る
            // rcodesign verify cannot check an ad-hoc signature without CMS, so it is skipped here; codesign --verify --deep --strict on the Mac always checks before distribution
            Debug.Log($"[MacAppAdHocSigner] rcodesign signed {appPath} (bundled ffmpeg present: {File.Exists(bundledFfmpeg)}); its verification runs as codesign --verify on the Mac");
            return true;

            #region Internal

            int Rcodesign(string arguments) => EditorProcessRunner.Run(rcodesignPath, arguments, Application.dataPath, "");

            #endregion
        }

        private static bool Fail(string message)
        {
            Debug.LogError("[MacAppAdHocSigner] " + message);
            return false;
        }
    }
}
