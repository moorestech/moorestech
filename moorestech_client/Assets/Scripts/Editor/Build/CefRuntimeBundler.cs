using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

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

    public static void Bundle(BuildTarget buildTarget, string playerOutputPath, bool isStrict)
    {
        // UPM解決済みパスからネイティブ成果物の実体を取得する
        // Resolve the native artifacts from the resolved UPM package path
        var packageInfo = PackageInfo.FindForAssetPath(PackageAssetPath);
        if (packageInfo == null)
        {
            Fail("CEF package 'jp.juha.cefunity' is not resolved", isStrict);
            return;
        }
        var pluginsSourceRoot = Path.Combine(packageInfo.resolvedPath, "Plugins");

        switch (buildTarget)
        {
            case BuildTarget.StandaloneOSX:
                BundleMacOs(Path.Combine(pluginsSourceRoot, "osx-arm64"), playerOutputPath, isStrict);
                break;
            case BuildTarget.StandaloneWindows64:
                BundleWindows(Path.Combine(pluginsSourceRoot, "win-x64"), playerOutputPath, isStrict);
                break;
            default:
                Fail($"CEF package has no native runtime for {buildTarget}", isStrict);
                break;
        }
    }

    private static void BundleMacOs(string sourceDirectory, string appPath, bool isStrict)
    {
        // helperの実体（LFS未解決の殻でないこと）を検証する
        // Verify the helper is real, not an unresolved LFS husk
        var helperSource = Path.Combine(sourceDirectory, "cef-unity-server.app");
        var helperExecutable = Path.Combine(helperSource, "Contents", "MacOS", "cef-unity-server");
        if (!File.Exists(helperExecutable) || new FileInfo(helperExecutable).Length < 1024)
        {
            Fail($"CEF helper app is missing or an LFS pointer: {helperExecutable}", isStrict);
            return;
        }

        // Unityが自動コピーしたdylibの隣がhelperの探索位置
        // The helper is looked up next to the dylib Unity auto-copied
        var dylibPaths = Directory.GetFiles(appPath, "libcef_unity_rust.dylib", SearchOption.AllDirectories);
        if (dylibPaths.Length == 0)
        {
            Fail($"libcef_unity_rust.dylib not found inside {appPath}", isStrict);
            return;
        }

        var helperDestination = Path.Combine(Path.GetDirectoryName(dylibPaths[0]), "cef-unity-server.app");
        if (Directory.Exists(helperDestination)) Directory.Delete(helperDestination, true);
        CopyDirectory(helperSource, helperDestination);
        Debug.Log($"[CefRuntimeBundler] bundled mac helper: {helperDestination}");
    }

    private static void BundleWindows(string sourceDirectory, string exePath, bool isStrict)
    {
        // helperの実体（LFS未解決の殻でないこと）を検証する
        // Verify the helper is real, not an unresolved LFS husk
        var helperSource = Path.Combine(sourceDirectory, "cef-unity-server.exe");
        if (!File.Exists(helperSource) || new FileInfo(helperSource).Length < 1024)
        {
            Fail($"CEF Windows helper is missing or an LFS pointer: {helperSource}", isStrict);
            return;
        }

        var dataDirectory = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath) + "_Data");
        var pluginsDestination = Path.Combine(dataDirectory, "Plugins", "x86_64");
        if (!Directory.Exists(pluginsDestination))
        {
            Fail($"Plugins/x86_64 not found in build output: {pluginsDestination}", isStrict);
            return;
        }

        // rust dllはUnityが自動コピー済みのため除いて配置する
        // Copy everything except the rust dll, which Unity already placed
        CopyDirectoryContents(sourceDirectory, pluginsDestination, "cef_unity_rust.dll");
        Debug.Log($"[CefRuntimeBundler] bundled windows runtime: {pluginsDestination}");
    }

    private static void Fail(string message, bool isStrict)
    {
        // strict時は動かない成果物を出さないため即失敗、CI互換時は現行どおり警告のみ
        // Strict mode fails fast to never ship a broken artifact; CI-compatible mode keeps warning-only
        if (isStrict) throw new BuildFailedException("[CefRuntimeBundler] " + message);
        Debug.LogWarning("[CefRuntimeBundler] " + message);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            if (file.EndsWith(".meta")) continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.StartsWith(".")) continue;
            CopyDirectory(directory, Path.Combine(destination, directoryName));
        }
    }

    private static void CopyDirectoryContents(string source, string destination, string excludedFileName)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(file);
            if (file.EndsWith(".meta") || fileName.Equals(excludedFileName, System.StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(destination, fileName), true);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.StartsWith(".")) continue;
            CopyDirectoryContents(directory, Path.Combine(destination, directoryName), excludedFileName);
        }
    }
}
