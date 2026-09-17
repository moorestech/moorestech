using System.IO;
using Client.Editor;
using Client.Game.InGame.BugReport.Recording;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// バグ報告の録画組み立てに使う ffmpeg を Windows 成果物へ同梱する
    /// Bundles the ffmpeg the bug-report video assembly uses into the Windows artifact
    /// ライセンス上、実行ファイルと一緒に配布物へライセンス文を並べる必要がある
    /// The license requires the license text to ship alongside the executable
    /// </summary>
    public static class FfmpegRuntimeBundler
    {
        private const string SourceExecutableName = "ffmpeg.exe";
        private const string SourceLicenseName = "LICENSE";
        private const string BundledLicenseName = "ffmpeg-LICENSE.txt";

        // 正本は非公開アセットリポジトリの ffmpeg/win-x64
        // The source of truth is ffmpeg/win-x64 in the private asset repository
        private static string SourceDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "PersonalAssets", "moorestech-client-private", "ffmpeg", "win-x64"));

        public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
        {
            // 同梱先はCEFランタイムと同じ Plugins/x86_64。実行時の探索位置を1箇所に揃える
            // Ships into Plugins/x86_64 next to the CEF runtime so runtime lookup has a single location
            if (buildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.Log($"[FfmpegRuntimeBundler] skipped for {buildTarget}");
                return;
            }

            var sourceExecutable = Path.Combine(SourceDirectory, SourceExecutableName);
            var sourceLicense = Path.Combine(SourceDirectory, SourceLicenseName);
            // 実体の検証（LFS未解決の殻でないこと）。CEF前例と同じ判定点を使う
            // Verify the executable is real, not an unresolved LFS husk, using the same check as the CEF precedent
            if (!File.Exists(sourceExecutable) || CefLfsPointer.IsPointerFile(sourceExecutable))
            {
                Fail($"ffmpeg executable is missing or an LFS pointer: {sourceExecutable}");
                return;
            }
            if (!File.Exists(sourceLicense))
            {
                Fail($"ffmpeg LICENSE is missing: {sourceLicense}");
                return;
            }

            var destinationDirectory = WindowsPlayerPluginsDirectory.Resolve(playerOutputPath);
            if (!Directory.Exists(destinationDirectory))
            {
                Fail($"Plugins/x86_64 not found in build output: {destinationDirectory}");
                return;
            }

            File.Copy(sourceExecutable, Path.Combine(destinationDirectory, FfmpegLocator.BundledWindowsExecutableName), true);
            File.Copy(sourceLicense, Path.Combine(destinationDirectory, BundledLicenseName), true);
            Debug.Log($"[FfmpegRuntimeBundler] bundled ffmpeg at {destinationDirectory}");

            #region Internal

            void Fail(string message)
            {
                // strict時は録画の組み立てができない成果物を配布しないため即失敗、CI互換時は警告のみ
                // Strict mode refuses to ship an artifact that cannot assemble video; CI-compatible mode only warns
                if (isStrict) throw new BuildFailedException("[FfmpegRuntimeBundler] " + message);
                Debug.LogWarning("[FfmpegRuntimeBundler] " + message);
            }

            #endregion
        }
    }
}
