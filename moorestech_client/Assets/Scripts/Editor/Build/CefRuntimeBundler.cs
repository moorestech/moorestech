using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Client.Editor.Build
{
    /// <summary>
    /// CEFネイティブランタイムをUPMのresolvedPathからPlayer成果物へ同梱する
    /// Bundles the CEF native runtime from the UPM resolved path into the Player artifact
    /// パッケージ同梱のCefBuildPostProcessorはAssets/CefUnity配置前提でUPMでは常に失敗し、
    /// しかも警告のみで成功扱いになるため、プロジェクト側で確実に同梱・検証する。
    /// The package's own CefBuildPostProcessor assumes Assets/CefUnity and always fails under UPM
    /// while only warning, so the project bundles and verifies the runtime itself.
    /// </summary>
    public static class CefRuntimeBundler
    {
        private const string PackageAssetPath = "Packages/jp.juha.cefunity/package.json";

        // rust dllはUnityが自動コピー済みなので二重配置しない
        // Unity already placed the rust dll, so it is never copied again
        private static readonly IReadOnlyList<string> WindowsExcludedFileNames = new[] { "cef_unity_rust.dll" };

        private static readonly IReadOnlyList<string> NoExcludedFileNames = new string[0];

        public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
        {
            // UPM解決済みパスからネイティブ成果物の実体を取得する
            // Resolve the native artifacts from the resolved UPM package path
            var packageInfo = PackageInfo.FindForAssetPath(PackageAssetPath);
            if (packageInfo == null)
            {
                Fail("CEF package 'jp.juha.cefunity' is not resolved");
                return;
            }
            var pluginsSourceRoot = Path.Combine(packageInfo.resolvedPath, "Plugins");

            switch (buildTarget)
            {
                case BuildTarget.StandaloneOSX:
                    BundleMacOs(Path.Combine(pluginsSourceRoot, "osx-arm64"));
                    break;
                case BuildTarget.StandaloneWindows64:
                    BundleWindows(Path.Combine(pluginsSourceRoot, "win-x64"));
                    break;
                default:
                    Fail($"CEF package has no native runtime for {buildTarget}");
                    break;
            }

            #region Internal

            void BundleMacOs(string sourceDirectory)
            {
                // helperの実体（LFS未解決の殻でないこと）を検証する
                // Verify the helper is real, not an unresolved LFS husk
                var helperSource = Path.Combine(sourceDirectory, "cef-unity-server.app");
                var helperExecutable = Path.Combine(helperSource, "Contents", "MacOS", "cef-unity-server");
                if (!File.Exists(helperExecutable) || CefLfsPointer.IsPointerFile(helperExecutable))
                {
                    Fail($"CEF helper app is missing or an LFS pointer: {helperExecutable}");
                    return;
                }

                // Unityが自動コピーしたdylibの隣がhelperの探索位置
                // The helper is looked up next to the dylib Unity auto-copied
                var dylibPaths = Directory.GetFiles(playerOutputPath, "libcef_unity_rust.dylib", SearchOption.AllDirectories);
                if (dylibPaths.Length == 0)
                {
                    Fail($"libcef_unity_rust.dylib not found inside {playerOutputPath}");
                    return;
                }

                var helperDestination = Path.Combine(Path.GetDirectoryName(dylibPaths[0]), "cef-unity-server.app");
                var copiedFileCount = DirectoryProcessor.CopyAndReplace(helperSource, helperDestination, NoExcludedFileNames);
                Debug.Log($"[CefRuntimeBundler] bundled mac helper: {copiedFileCount} files at {helperDestination}");
            }

            void BundleWindows(string sourceDirectory)
            {
                // helperの実体（LFS未解決の殻でないこと）を検証する
                // Verify the helper is real, not an unresolved LFS husk
                var helperSource = Path.Combine(sourceDirectory, "cef-unity-server.exe");
                if (!File.Exists(helperSource) || CefLfsPointer.IsPointerFile(helperSource))
                {
                    Fail($"CEF Windows helper is missing or an LFS pointer: {helperSource}");
                    return;
                }

                var dataDirectory = Path.Combine(Path.GetDirectoryName(playerOutputPath), Path.GetFileNameWithoutExtension(playerOutputPath) + "_Data");
                var pluginsDestination = Path.Combine(dataDirectory, "Plugins", "x86_64");
                if (!Directory.Exists(pluginsDestination))
                {
                    Fail($"Plugins/x86_64 not found in build output: {pluginsDestination}");
                    return;
                }

                // Unity配置済みのプラグインを消さないよう、削除せず上書きコピーする
                // Overwrite without deleting so the plugins Unity already placed survive
                var copiedFileCount = DirectoryProcessor.Copy(sourceDirectory, pluginsDestination, WindowsExcludedFileNames);
                Debug.Log($"[CefRuntimeBundler] bundled windows runtime: {copiedFileCount} files at {pluginsDestination}");
            }

            void Fail(string message)
            {
                // strict時は動かない成果物を出さないため即失敗、CI互換時は現行どおり警告のみ
                // Strict mode fails fast to never ship a broken artifact; CI-compatible mode keeps warning-only
                if (isStrict) throw new BuildFailedException("[CefRuntimeBundler] " + message);
                Debug.LogWarning("[CefRuntimeBundler] " + message);
            }

            #endregion
        }
    }
}
