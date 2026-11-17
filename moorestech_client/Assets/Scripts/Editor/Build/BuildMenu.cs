using UnityEditor;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルドのメニュー入口（対話でDevelopment可否と出力先を決める）
    /// Menu entries for Player builds; dialogs decide Development mode and the output directory
    /// </summary>
    public static class BuildMenu
    {
        private const string OutputPathKey = "WindowsBuildOutputPath";

        [MenuItem("moorestech/Build/WindowsBuild")]
        public static void WindowsBuild()
        {
            BuildInteractive(BuildTarget.StandaloneWindows64, AsksDevelopmentBuild());
        }

        [MenuItem("moorestech/Build/MacOsBuild")]
        public static void MacOsBuild()
        {
            BuildInteractive(BuildTarget.StandaloneOSX, AsksDevelopmentBuild());
        }

        // 再起動ループ同梱・Release固定
        // Bundles the restart loop, always Release
        [MenuItem("moorestech/Build/MacOsExhibitionBuild")]
        public static void MacOsExhibitionBuild()
        {
            var outputDirectory = SelectOutputDirectory(BuildTarget.StandaloneOSX);
            if (outputDirectory == null) return;

            ReportOutcome(BuildPipeline.Execute(PlayerBuildRequest.ForExhibition(outputDirectory)), outputDirectory);
        }

        // 手元焼き。無人入口と同用途
        // Manual bake; same purpose as the unattended entry
        [MenuItem("moorestech/Build/WindowsSteamPlaytestBuild")]
        public static void WindowsSteamPlaytestBuild()
        {
            BuildSteamPlaytestInteractive(BuildTarget.StandaloneWindows64);
        }

        [MenuItem("moorestech/Build/MacOsSteamPlaytestBuild")]
        public static void MacOsSteamPlaytestBuild()
        {
            BuildSteamPlaytestInteractive(BuildTarget.StandaloneOSX);
        }

        [MenuItem("moorestech/Build/LinuxBuild")]
        public static void LinuxBuild()
        {
            // Linuxクライアントは配布対象外のため、入口は意図的に失敗させてダイアログで明示する（.decisions/2026-08-02）
            // The Linux client is not distributed, so this entry fails on purpose and says so in a dialog
            EditorUtility.DisplayDialog(
                "Linux Build",
                "Linuxクライアントは配布対象外のため、このメニューは意図的に失敗します（再開は統合計画 §5.2 で別に判断します）。",
                "閉じる");
            Debug.LogError("[BuildMenu] LinuxBuild は配布対象外のため意図的に失敗させました（再開は統合計画 §5.2 で別に判断する）");
        }

        private static bool AsksDevelopmentBuild()
        {
            return EditorUtility.DisplayDialog(
                "Build Configuration",
                "Development Buildで実行しますか？",
                "Development Build",
                "Release Build");
        }

        private static void BuildInteractive(BuildTarget buildTarget, bool isDevelopmentBuild)
        {
            var outputDirectory = SelectOutputDirectory(buildTarget);
            if (outputDirectory == null) return;

            // 開発用は同梱失敗を警告で続行
            // Dev builds warn and continue on bundling failures
            var outcome = BuildPipeline.Execute(PlayerBuildRequest.ForLocalDevelopment(buildTarget, outputDirectory, isDevelopmentBuild));

            ReportOutcome(outcome, outputDirectory);
        }

        private static void BuildSteamPlaytestInteractive(BuildTarget buildTarget)
        {
            var outputDirectory = SelectOutputDirectory(buildTarget);
            if (outputDirectory == null) return;

            ReportOutcome(BuildPipeline.Execute(PlayerBuildRequest.ForSteamPlaytest(buildTarget, outputDirectory)), outputDirectory);
        }

        // 出力先を選択する（前回パスを記憶）。キャンセル時はnull
        // Choose the output directory, remembering the previous path; null on cancel
        private static string SelectOutputDirectory(BuildTarget buildTarget)
        {
            var playerPrefsKey = OutputPathKey + buildTarget;
            var outputDirectory = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");
            if (outputDirectory == string.Empty) return null;
            PlayerPrefs.SetString(playerPrefsKey, outputDirectory);
            PlayerPrefs.Save();
            return outputDirectory;
        }

        // 失敗した成果物をFinderで開いて成功に見せない
        // Never reveal a failed artifact as if the build had succeeded
        private static void ReportOutcome(PlayerBuildOutcome outcome, string outputDirectory)
        {
            switch (outcome)
            {
                case PlayerBuildOutcome.Succeeded:
                    EditorUtility.RevealInFinder(outputDirectory);
                    break;
                case PlayerBuildOutcome.BuildTargetSwitchFailed:
                    EditorUtility.DisplayDialog("Build Failed", "ビルドターゲットの切り替えに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.MacArchitecturePinFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Macのarm64固定に失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.AddressablesBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Addressablesのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.PlayerBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Playerのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.MacSigningFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Mac成果物のad-hoc署名に失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.PreRemovalStreamingAssetsCleanupFailed:
                    EditorUtility.DisplayDialog("Build Failed", "撤去前のビルドが残したStreamingAssetsの成果物を消せませんでした。Consoleのエラーを確認してください。", "OK");
                    break;
            }
        }
    }
}
