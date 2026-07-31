using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Playerビルドの単一入口（メニュー・CI共通のオーケストレーション）
/// Single entry for Player builds; orchestration shared by menu and CI
/// </summary>
public class BuildPipeline
{
    private const string OutputPathKey = "WindowsBuildOutputPath";

    [MenuItem("moorestech/Build/WindowsBuild")]
    public static void WindowsBuild()
    {
        BuildInteractive(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("moorestech/Build/MacOsBuild")]
    public static void MacOsBuild()
    {
        BuildInteractive(BuildTarget.StandaloneOSX);
    }

    [MenuItem("moorestech/Build/LinuxBuild")]
    public static void LinuxBuild()
    {
        BuildInteractive(BuildTarget.StandaloneLinux64);
    }

    private static void BuildInteractive(BuildTarget buildTarget)
    {
        // Development Buildかどうかを選択する
        // Select whether to use Development Build
        var isDevelopmentBuild = EditorUtility.DisplayDialog(
            "Build Configuration",
            "Development Buildで実行しますか？",
            "Development Build",
            "Release Build");

        // 出力先を選択する（前回パスを記憶）
        // Choose the output directory, remembering the previous path
        var playerPrefsKey = OutputPathKey + buildTarget;
        var outputDirectory = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");
        if (outputDirectory == string.Empty) return;
        PlayerPrefs.SetString(playerPrefsKey, outputDirectory);
        PlayerPrefs.Save();

        // ローカル配布用: 同梱失敗は即失敗・ゲームデータ必須
        // Local distribution: bundling problems fail the build and game data is mandatory
        Execute(new PlayerBuildRequest
        {
            Target = buildTarget,
            OutputDirectory = outputDirectory,
            IsDevelopmentBuild = isDevelopmentBuild,
            IsStrictBundling = true,
            BundleLocalGameData = true,
            ExitOnFinish = false,
        });

        EditorUtility.RevealInFinder(outputDirectory);
    }

    private static void Execute(PlayerBuildRequest request)
    {
        Debug.Log("Build Start Time : " + DateTime.Now);
        var buildStartTime = DateTime.Now;

        var buildOptionsFlags = request.IsDevelopmentBuild
            ? BuildOptions.Development
            : BuildOptions.CompressWithLz4;

        var buildOptions = new BuildPlayerOptions
        {
            target = request.Target,
            locationPathName = Path.Combine(request.OutputDirectory, PlayerExecutableName(request.Target)),
            scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
            options = buildOptionsFlags,
        };

        // Addressablesコンテンツをクリーンビルドする
        // Clean build Addressables content before building the player
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
        if (!string.IsNullOrEmpty(addressablesResult.Error))
        {
            Debug.LogError("Addressables Build Failed: " + addressablesResult.Error);
            if (request.ExitOnFinish) EditorApplication.Exit(1);
            return;
        }
        Debug.Log("Addressables Build Succeeded: " + addressablesResult.OutputPath);

        var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
        Debug.Log("Build Result :" + report.summary.result);

        // 成功時のみ、動作に必要なCEFランタイムとゲームデータを同梱する
        // Only on success, bundle the CEF runtime and game data the player needs to run
        if (report.summary.result == BuildResult.Succeeded)
        {
            CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, request.IsStrictBundling);
            if (request.BundleLocalGameData) GameDataBundler.Bundle(request.OutputDirectory, request.IsStrictBundling);
        }

        Debug.Log("Build Output Path :" + report.summary.outputPath);
        Debug.Log("Build Summary TotalSize :" + report.summary.totalSize);
        Debug.Log("Build Finish Time : " + DateTime.Now);
        Debug.Log("Build Time : " + (DateTime.Now - buildStartTime).ToString(@"hh\:mm\:ss"));

        if (request.ExitOnFinish) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string PlayerExecutableName(BuildTarget buildTarget)
    {
        // OSごとの配布実行ファイル名を明示する
        // Explicit per-OS distributable executable name
        switch (buildTarget)
        {
            case BuildTarget.StandaloneWindows64: return "moorestech.exe";
            case BuildTarget.StandaloneOSX: return "moorestech.app";
            default: return "moorestech";
        }
    }

    #region from Github Action

    public static void WindowsBuildFromGithubAction()
    {
        BuildFromGithubAction(BuildTarget.StandaloneWindows64);
    }

    public static void MacOsBuildFromGithubAction()
    {
        BuildFromGithubAction(BuildTarget.StandaloneOSX);
    }

    public static void LinuxBuildFromGithubAction()
    {
        BuildFromGithubAction(BuildTarget.StandaloneLinux64);
    }

    private static void BuildFromGithubAction(BuildTarget buildTarget)
    {
        // CI入口: 現行契約を維持（Output_<target>固定・警告のみの同梱・ゲームデータ無し）
        // CI entry keeps the current contract: fixed Output_<target>, warn-only bundling, no game data
        Execute(new PlayerBuildRequest
        {
            Target = buildTarget,
            OutputDirectory = "Output_" + buildTarget,
            IsDevelopmentBuild = true,
            IsStrictBundling = false,
            BundleLocalGameData = false,
            ExitOnFinish = true,
        });
    }

    #endregion
}
