using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildPipeline
{
    private const string OutputPathKey = "ServerBuildOutputPath";

    #region Menu Items - GUI Build

    [MenuItem("moorestech/Server Build/Windows Build")]
    public static void WindowsBuild()
    {
        GUIBuildPipeline(BuildTarget.StandaloneWindows64, false, true);
    }

    [MenuItem("moorestech/Server Build/MacOS Build")]
    public static void MacOsBuild()
    {
        GUIBuildPipeline(BuildTarget.StandaloneOSX, false, true);
    }

    [MenuItem("moorestech/Server Build/Linux Build")]
    public static void LinuxBuild()
    {
        GUIBuildPipeline(BuildTarget.StandaloneLinux64, false, true);
    }

    #endregion

    #region Menu Items - Dedicated Server Build

    [MenuItem("moorestech/Server Build/Windows Dedicated Server")]
    public static void WindowsDedicatedServer()
    {
        DedicatedServerPipeline(BuildTarget.StandaloneWindows64, false, true);
    }

    [MenuItem("moorestech/Server Build/Linux Dedicated Server")]
    public static void LinuxDedicatedServer()
    {
        DedicatedServerPipeline(BuildTarget.StandaloneLinux64, false, true);
    }

    #endregion

    #region CI Entry Points - GUI Build

    public static void WindowsBuildFromGithubAction()
    {
        GUIBuildPipeline(BuildTarget.StandaloneWindows64, true, false);
    }

    public static void MacOsBuildFromGithubAction()
    {
        GUIBuildPipeline(BuildTarget.StandaloneOSX, true, false);
    }

    public static void LinuxBuildFromGithubAction()
    {
        GUIBuildPipeline(BuildTarget.StandaloneLinux64, true, false);
    }

    #endregion

    #region CI Entry Points - Dedicated Server Build

    public static void WindowsDedicatedServerFromGithubAction()
    {
        DedicatedServerPipeline(BuildTarget.StandaloneWindows64, true, false);
    }

    public static void LinuxDedicatedServerFromGithubAction()
    {
        DedicatedServerPipeline(BuildTarget.StandaloneLinux64, true, false);
    }

    #endregion

    #region Internal

    // GUI版ビルドパイプライン
    // GUI build pipeline (standard player)
    private static void GUIBuildPipeline(BuildTarget buildTarget, bool isErrorExit, bool isSelectOutputPath)
    {
        var outputDir = "Output_" + buildTarget;
        ExecuteBuild(buildTarget, StandaloneBuildSubtarget.Player, outputDir, isErrorExit, isSelectOutputPath);
    }

    // Dedicated Serverビルドパイプライン
    // Dedicated Server build pipeline (headless)
    private static void DedicatedServerPipeline(BuildTarget buildTarget, bool isErrorExit, bool isSelectOutputPath)
    {
        var outputDir = "Output_DedicatedServer_" + buildTarget;
        ExecuteBuild(buildTarget, StandaloneBuildSubtarget.Server, outputDir, isErrorExit, isSelectOutputPath);
    }

    private static void ExecuteBuild(BuildTarget buildTarget, StandaloneBuildSubtarget subtarget, string defaultOutputDir, bool isErrorExit, bool isSelectOutputPath)
    {
        Debug.Log("Build Start Time : " + DateTime.Now);
        var buildStartTime = DateTime.Now;

        // Development Buildかどうかを選択する
        // Select whether to use Development Build
        var isDevelopmentBuild = false;
        if (isSelectOutputPath)
        {
            isDevelopmentBuild = EditorUtility.DisplayDialog(
                "Build Configuration",
                "Development Buildで実行しますか？",
                "Development Build",
                "Release Build");
        }

        var path = defaultOutputDir;
        if (isSelectOutputPath)
        {
            var playerPrefsKey = OutputPathKey + buildTarget + subtarget;
            path = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");

            if (path == string.Empty) return;

            PlayerPrefs.SetString(playerPrefsKey, path);
            PlayerPrefs.Save();
        }

        // ビルドサブターゲットを設定（PlayerまたはServer）
        // Set build subtarget (Player or Server for dedicated server)
        var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
        EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;

        // バッチモードの場合はメモリ効率を優先して Development Build を使用
        // Use Development Build in batch mode to reduce memory consumption
        var buildOptionsFlags = isDevelopmentBuild || !isSelectOutputPath
            ? BuildOptions.Development
            : BuildOptions.CompressWithLz4;

        var executableName = buildTarget == BuildTarget.StandaloneWindows64
            ? "/moorestech_server.exe"
            : "/moorestech_server";

        var buildOptions = new BuildPlayerOptions
        {
            target = buildTarget,
            subtarget = (int)subtarget,
            locationPathName = path + executableName,
            scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
            options = buildOptionsFlags,
        };

        var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);

        // ビルドサブターゲットを元に戻す
        // Restore previous build subtarget
        EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;

        if (isSelectOutputPath) EditorUtility.RevealInFinder(path);

        Debug.Log("Build Result :" + report.summary.result);
        Debug.Log("Build Output Path :" + report.summary.outputPath);
        Debug.Log("Build Summary TotalSize :" + report.summary.totalSize);
        Debug.Log("Build Finish Time : " + DateTime.Now);

        // ビルドにかかった時間を hh:mm:ss で表示
        // Display build duration in hh:mm:ss format
        Debug.Log("Build Time : " + (DateTime.Now - buildStartTime).ToString(@"hh\:mm\:ss"));

        if (isErrorExit) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    #endregion
}
