using System.IO;
using Client.Editor;
using Client.Game.InGame.BugReport.Recording;
using Client.WebUiHost.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build.Bundlers
{
    /// <summary>
    /// 録画用ffmpegをWin/Mac成果物へ同梱する
    /// Bundles recording ffmpeg into Win/Mac artifacts
    /// GPLのため実行ファイルとライセンス文を同じ配布物へ入れる
    /// GPL requires the license text to ship with the executable
    /// </summary>
    public static class FfmpegRuntimeBundler
    {
        private const string SourceLicenseName = "LICENSE";
        private const string BundledLicenseName = "ffmpeg-LICENSE.txt";

        // 正本は非公開アセットのffmpeg/<os-arch>
        // The source of truth is ffmpeg/<os-arch> in the private asset repository
        private static string SourceDirectory(string platformDirectoryName) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "PersonalAssets", "moorestech-client-private", "ffmpeg", platformDirectoryName));

        public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows64:
                    BundleWindows();
                    break;
                case BuildTarget.StandaloneOSX:
                    BundleMacOs();
                    break;
                default:
                    Debug.Log($"[FfmpegRuntimeBundler] skipped for {buildTarget}");
                    break;
            }

            #region Internal

            void BundleWindows()
            {
                // CEFと同じPluginsへ置く
                // Place it in Plugins/x86_64 beside the CEF runtime
                var destinationDirectory = WindowsPlayerPluginsDirectory.Resolve(playerOutputPath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Fail($"Plugins/x86_64 not found in build output: {destinationDirectory}");
                    return;
                }

                CopyExecutableAndLicense(SourceDirectory("win-x64"), FfmpegLocator.BundledWindowsExecutableName,
                    Path.Combine(destinationDirectory, FfmpegLocator.BundledWindowsExecutableName),
                    Path.Combine(destinationDirectory, BundledLicenseName));
            }

            void BundleMacOs()
            {
                // 実行→MacOS、ライセンス→Resources
                // Executable goes to MacOS, license to Resources
                var executableDestination = FfmpegLocator.ResolveBundledMacExecutablePath(playerOutputPath);
                if (!Directory.Exists(Path.GetDirectoryName(executableDestination)))
                {
                    Fail($"Contents/MacOS not found in build output: {Path.GetDirectoryName(executableDestination)}");
                    return;
                }

                var contentsDirectory = Path.Combine(playerOutputPath, "Contents");
                if (!CopyExecutableAndLicense(SourceDirectory("macos-arm64"), FfmpegLocator.BundledMacExecutableName,
                        executableDestination, Path.Combine(contentsDirectory, "Resources", BundledLicenseName))) return;

                // コピー後に実行権を保証する
                // Ensure the executable bit after copying
                if (EditorProcessRunner.Run("/bin/chmod", $"+x \"{executableDestination}\"", Application.dataPath, "") != 0)
                    Fail($"chmod failed: {executableDestination}");
            }

            bool CopyExecutableAndLicense(string sourceDirectory, string executableName, string executableDestination, string licenseDestination)
            {
                var sourceExecutable = Path.Combine(sourceDirectory, executableName);
                var sourceLicense = Path.Combine(sourceDirectory, SourceLicenseName);

                // 未取得のLFSポインタは実行できないため拒否する
                // Reject an unresolved LFS pointer, which cannot execute
                if (!File.Exists(sourceExecutable) || CefLfsPointer.IsPointerFile(sourceExecutable))
                {
                    Fail($"ffmpeg executable is missing or an LFS pointer: {sourceExecutable}");
                    return false;
                }
                if (!File.Exists(sourceLicense))
                {
                    Fail($"ffmpeg LICENSE is missing: {sourceLicense}");
                    return false;
                }

                File.Copy(sourceExecutable, executableDestination, true);
                File.Copy(sourceLicense, licenseDestination, true);
                Debug.Log($"[FfmpegRuntimeBundler] bundled ffmpeg at {executableDestination}");
                return true;
            }

            void Fail(string message)
            {
                // ffmpeg無しでは録画を組み立てられないためstrictは停止
                // Strict stops because recording cannot be assembled without ffmpeg
                if (isStrict) throw new BuildFailedException("[FfmpegRuntimeBundler] " + message);
                Debug.LogWarning("[FfmpegRuntimeBundler] " + message);
            }

            #endregion
        }
    }
}
