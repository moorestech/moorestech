using System;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    // UITK段階0のPoCビルド専用（poc/uitk-stage0-measure）。ローカル開発用のRelease構成をbatchmodeで焼く
    // PoC builds for UITK stage 0 only (poc/uitk-stage0-measure): bakes the local-development Release configuration in batchmode
    public static class UitkStage0PocBuildCli
    {
        public static void WindowsLocalRelease()
        {
            Run(BuildTarget.StandaloneWindows64);
        }

        public static void MacLocalRelease()
        {
            Run(BuildTarget.StandaloneOSX);
        }

        // GUIのEditorからuloop経由で焼く入口（終了しない）
        // Entry for baking from the GUI Editor through uloop (does not exit)
        public static string BuildWithoutExit(BuildTarget target, string outputDirectory)
        {
            return BuildPipeline.Execute(PlayerBuildRequest.ForLocalDevelopment(target, outputDirectory, false)).ToString();
        }

        private static void Run(BuildTarget target)
        {
            // 出力先は環境変数で受ける。無ければ焼かずに失敗で終える
            // The output directory comes from the environment; without it, exit as a failure without building
            var outputDirectory = Environment.GetEnvironmentVariable("UITK_POC_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(outputDirectory))
            {
                Debug.LogError("[UitkStage0PocBuildCli] UITK_POC_BUILD_OUTPUT is not set");
                EditorApplication.Exit(2);
                return;
            }
            var outcome = BuildPipeline.Execute(PlayerBuildRequest.ForLocalDevelopment(target, outputDirectory, false));
            Debug.Log($"[UitkStage0PocBuildCli] {target} outcome={outcome} output={outputDirectory}");
            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }
    }
}
