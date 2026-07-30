using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Client.Editor.StandaloneQa
{
    public static class StandaloneTerrainQaBuildPipeline
    {
        public static void BuildMacOs()
        {
            // 同一Editor assemblyのクライアント用CI入口へ委譲し、サーバー側の同名型を除外する
            // Delegate to the client CI entry in this Editor assembly, excluding the same-named server type
            var clientPipelineType = typeof(StandaloneTerrainQaBuildPipeline).Assembly.GetType("BuildPipeline");
            var buildMethod = clientPipelineType.GetMethod(
                "MacOsBuildFromGithubAction",
                BindingFlags.Public | BindingFlags.Static);
            buildMethod.Invoke(null, null);
        }

        public static void RebuildMacOsPlayer()
        {
            // Addressables成功後のコード修正だけを既存bundleへ重ね、Player検証の反復時間を短縮する
            // Rebuild code changes over the successful Addressables bundles to shorten Player QA iterations
            var buildOptions = new BuildPlayerOptions
            {
                target = BuildTarget.StandaloneOSX,
                locationPathName = "Output_StandaloneOSX/moorestech",
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                options = BuildOptions.Development,
            };

            var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
