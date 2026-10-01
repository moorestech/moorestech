using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// release-playtest.sh用の無人入口
    /// The unattended entry called by release-playtest.sh
    /// </summary>
    public static class SteamPlaytestBuildCli
    {
        private const string OutputDirectoryEnvKey = "MOORESTECH_BUILD_OUTPUT";

        public static void WindowsSteamPlaytestBuild()
        {
            BuildFromEnvironment(BuildTarget.StandaloneWindows64);
        }

        public static void MacOsSteamPlaytestBuild()
        {
            BuildFromEnvironment(BuildTarget.StandaloneOSX);
        }

        private static void BuildFromEnvironment(BuildTarget target)
        {
            // 出力先未指定で走らせるとカレント直下を汚すため、理由を残して拒否する
            // Running without an output directory would pollute the CWD, so refuse and log why
            var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvKey);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Debug.LogError($"[SteamPlaytestBuildCli] {OutputDirectoryEnvKey} が未設定のためビルドしません");
                EditorApplication.Exit(2);
                return;
            }

            var outcome = BuildPipeline.Execute(PlayerBuildRequest.ForSteamPlaytest(target, outputDirectory));
            Debug.Log($"[SteamPlaytestBuildCli] outcome:{outcome} target:{target} output:{outputDirectory}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
