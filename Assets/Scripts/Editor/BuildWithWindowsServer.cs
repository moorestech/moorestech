using System.IO;
using System.Linq;
using GameConst;
using UnityEditor;
using UnityEngine;


public class BuildWithWindowsServer
{
    private const string OutputPathKey = "WindowsBuildOutputPath";
    [MenuItem("moorestech/WindowsBuild")]
    public static void TestBuild()
    {
        var path = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(OutputPathKey,""), 
            "");

        if (path == string.Empty)
        {
            return;
        }
        
        PlayerPrefs.SetString(OutputPathKey, path);
        PlayerPrefs.Save();


        DirectoryProcessor.CopyAndReplace(ServerConst.ServerDirectory, Path.Combine(path,ServerConst.StandAloneServerDirectory));
            
        var buildOptions = new BuildPlayerOptions();
        buildOptions.target = BuildTarget.StandaloneWindows;
        buildOptions.locationPathName = path + "/moorestech.exe";
        buildOptions.scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            
        BuildPipeline.BuildPlayer(buildOptions);
        
        EditorUtility.RevealInFinder( path );
    }
}