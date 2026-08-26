using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// ローカル配布相当（Release・strict同梱・ゲームデータ込み）をbatchmodeから起動するQA専用入口
    /// QA-only entry that runs the local-distribution build (Release, strict bundling, game data) from batchmode
    /// </summary>
    public static class ReleaseLocalBuildCli
    {
        public static void MacOsReleaseLocalBuild()
        {
            var outputDirectory = Environment.GetEnvironmentVariable("MOORESTECH_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(outputDirectory))
            {
                Debug.LogError("[ReleaseLocalBuildCli] MOORESTECH_BUILD_OUTPUT is not set");
                EditorApplication.Exit(2);
                return;
            }

            var request = new PlayerBuildRequest
            {
                Target = BuildTarget.StandaloneOSX,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = false,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            };

            var outcome = BuildPipeline.Execute(request);
            Debug.Log($"[ReleaseLocalBuildCli] outcome={outcome}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
