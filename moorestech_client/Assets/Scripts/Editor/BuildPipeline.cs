using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildPipeline
{
    private const string OutputPathKey = "WindowsBuildOutputPath";
    
    [MenuItem("moorestech/Build/WindowsBuild")]
    public static void WindowsBuild()
    {
        Pipeline(BuildTarget.StandaloneWindows64, false, true);
    }
    
    [MenuItem("moorestech/Build/MacOsBuild")]
    public static void MacOsBuild()
    {
        Pipeline(BuildTarget.StandaloneOSX, false, true);
    }
    
    [MenuItem("moorestech/Build/LinuxBuild")]
    public static void LinuxBuild()
    {
        Pipeline(BuildTarget.StandaloneLinux64, false, true);
    }
    
    private static void Pipeline(BuildTarget buildTarget, bool isErrorExit, bool isSelectOutputPath)
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

        var path = "Output_" + buildTarget;
        if (isSelectOutputPath)
        {
            var playerPrefsKey = OutputPathKey + buildTarget;
            path = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");

            if (path == string.Empty) return;

            PlayerPrefs.SetString(playerPrefsKey, path);
            PlayerPrefs.Save();
        }


        //DirectoryProcessor.CopyAndReplace(ServerConst.ServerDirectory, Path.Combine(path, ServerConst.ServerDirName));

        // バッチモードの場合はメモリ効率を優先して Development Build を使用
        // Use Development Build in batch mode to reduce memory consumption
        var buildOptionsFlags = isDevelopmentBuild || !isSelectOutputPath
            ? BuildOptions.Development
            : BuildOptions.CompressWithLz4;

        var buildOptions = new BuildPlayerOptions
        {
            target = buildTarget,
            locationPathName = path + (buildTarget == BuildTarget.StandaloneWindows64 ? "/moorestech.exe" : "/moorestech"),
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
            if (isErrorExit) EditorApplication.Exit(1);
            return;
        }
        Debug.Log("Addressables Build Succeeded: " + addressablesResult.OutputPath);

        var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
        
        if (isSelectOutputPath) EditorUtility.RevealInFinder(path);
        
        Debug.Log("Build Result :" + report.summary.result);
        
        Debug.Log("Build Output Path :" + report.summary.outputPath);
        Debug.Log("Build Summary TotalSize :" + report.summary.totalSize);
        
        Debug.Log("Build Finish Time : " + DateTime.Now);
        //ビルドにかかった時間を hh:mm:ss で表示
        Debug.Log("Build Time : " + (DateTime.Now - buildStartTime).ToString(@"hh\:mm\:ss"));
        
        
        if (isErrorExit) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    
    #region from Github Action
    
    public static void WindowsBuildFromGithubAction()
    {
        Pipeline(BuildTarget.StandaloneWindows64, true, false);
    }
    
    public static void MacOsBuildFromGithubAction()
    {
        Pipeline(BuildTarget.StandaloneOSX, true, false);
    }
    
    public static void LinuxBuildFromGithubAction()
    {
        Pipeline(BuildTarget.StandaloneLinux64, true, false);
    }
    
    #endregion
}