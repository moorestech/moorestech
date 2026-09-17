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

        // 展示会などの配布用。Development/Releaseを聞かずRelease固定で焼く
        // For distribution such as exhibitions: no Development prompt, always Release
        [MenuItem("moorestech/Build/MacOsReleaseLocalBuild")]
        public static void MacOsReleaseLocalBuild()
        {
            BuildReleaseLocalInteractive(BuildTarget.StandaloneOSX);
        }

        // プレイテスト配布用のWindows成果物。契約はmac版と同一
        // The Windows artifact for playtest distribution; same contract as the mac entry
        [MenuItem("moorestech/Build/WindowsReleaseLocalBuild")]
        public static void WindowsReleaseLocalBuild()
        {
            BuildReleaseLocalInteractive(BuildTarget.StandaloneWindows64);
        }

        [MenuItem("moorestech/Build/LinuxBuild")]
        public static void LinuxBuild()
        {
            // LinuxはCEFネイティブランタイムが無く同梱検証で必ず失敗するため、着手前に明示して同意を取る
            // Linux has no CEF native runtime and always fails bundling, so state it and confirm before starting
            var continuesAnyway = EditorUtility.DisplayDialog(
                "Linux Build",
                "LinuxにはCEFネイティブランタイムが提供されていないため、同梱検証で必ず失敗します。それでも実行しますか？",
                "実行する",
                "やめる");
            if (!continuesAnyway) return;

            BuildInteractive(BuildTarget.StandaloneLinux64, AsksDevelopmentBuild());
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

            // ローカル配布用: 同梱失敗は即失敗・ゲームデータ必須
            // Local distribution: bundling problems fail the build and game data is mandatory
            var outcome = BuildPipeline.Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = isDevelopmentBuild,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            });

            ReportOutcome(outcome, outputDirectory);
        }

        private static void BuildReleaseLocalInteractive(BuildTarget buildTarget)
        {
            var outputDirectory = SelectOutputDirectory(buildTarget);
            if (outputDirectory == null) return;

            ReportOutcome(BuildPipeline.Execute(ReleaseLocalBuildCli.CreateRequest(buildTarget, outputDirectory)), outputDirectory);
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
                case PlayerBuildOutcome.AddressablesBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Addressablesのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
                case PlayerBuildOutcome.PlayerBuildFailed:
                    EditorUtility.DisplayDialog("Build Failed", "Playerのビルドに失敗しました。Consoleのエラーを確認してください。", "OK");
                    break;
            }
        }
    }
}
