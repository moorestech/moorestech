using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Release固定のローカル配布ビルドの契約と、無人（batchmode）入口
    /// The Release-fixed local distribution build contract and its unattended (batchmode) entry
    /// </summary>
    public static class ReleaseLocalBuildCli
    {
        public const string OutputDirectoryEnvKey = "MOORESTECH_BUILD_OUTPUT";

        // GUIメニューとbatchmodeで同一の契約を使い、入口ごとの設定差を構造的に消す
        // Menu and batchmode share one contract so per-entry setting drift cannot happen
        public static PlayerBuildRequest CreateRequest(BuildTarget target, string outputDirectory)
        {
            return new PlayerBuildRequest
            {
                Target = target,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = false,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            };
        }

        // Mac miniのrelease-playtest.shが -executeMethod で呼ぶ無人入口
        // The unattended entry release-playtest.sh calls on the Mac mini via -executeMethod
        public static void WindowsReleaseLocalBuild()
        {
            Run(BuildTarget.StandaloneWindows64);
        }

        private static void Run(BuildTarget target)
        {
            // 出力先未指定で走らせるとカレント直下を汚すため、理由を残して拒否する
            // Running without an output directory would pollute the CWD, so refuse and log why
            var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvKey);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Debug.LogError($"[ReleaseLocalBuildCli] {OutputDirectoryEnvKey} が未設定のためビルドしません");
                EditorApplication.Exit(2);
                return;
            }

            var outcome = BuildPipeline.Execute(CreateRequest(target, outputDirectory));
            Debug.Log($"[ReleaseLocalBuildCli] outcome:{outcome} target:{target} output:{outputDirectory}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
