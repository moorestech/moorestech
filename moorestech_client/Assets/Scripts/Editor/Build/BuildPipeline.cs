using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルドの単一入口（メニュー・CI共通のオーケストレーション）
    /// Single entry for Player builds; orchestration shared by menu and CI
    /// </summary>
    public class BuildPipeline
    {
        private const string OutputPathKey = "WindowsBuildOutputPath";

        [MenuItem("moorestech/Build/WindowsBuild")]
        public static void WindowsBuild()
        {
            BuildInteractive(BuildTarget.StandaloneWindows64);
        }

        [MenuItem("moorestech/Build/MacOsBuild")]
        public static void MacOsBuild()
        {
            BuildInteractive(BuildTarget.StandaloneOSX);
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

            BuildInteractive(BuildTarget.StandaloneLinux64);
        }

        private static void BuildInteractive(BuildTarget buildTarget)
        {
            // Development Buildかどうかを選択する
            // Select whether to use Development Build
            var isDevelopmentBuild = EditorUtility.DisplayDialog(
                "Build Configuration",
                "Development Buildで実行しますか？",
                "Development Build",
                "Release Build");

            // 出力先を選択する（前回パスを記憶）
            // Choose the output directory, remembering the previous path
            var playerPrefsKey = OutputPathKey + buildTarget;
            var outputDirectory = EditorUtility.OpenFolderPanel("Build", PlayerPrefs.GetString(playerPrefsKey, ""), "");
            if (outputDirectory == string.Empty) return;
            PlayerPrefs.SetString(playerPrefsKey, outputDirectory);
            PlayerPrefs.Save();

            // ローカル配布用: 同梱失敗は即失敗・ゲームデータ必須
            // Local distribution: bundling problems fail the build and game data is mandatory
            var outcome = Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = outputDirectory,
                IsDevelopmentBuild = isDevelopmentBuild,
                IsStrictBundling = true,
                BundleLocalGameData = true,
            });

            // 失敗した成果物をFinderで開いて成功に見せない
            // Never reveal a failed artifact as if the build had succeeded
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

        private static PlayerBuildOutcome Execute(PlayerBuildRequest request)
        {
            Debug.Log("Build Start Time : " + DateTime.Now);
            var buildStartTime = DateTime.Now;

            var buildOptionsFlags = request.IsDevelopmentBuild
                ? BuildOptions.Development
                : BuildOptions.CompressWithLz4;

            var buildOptions = new BuildPlayerOptions
            {
                target = request.Target,
                locationPathName = Path.Combine(request.OutputDirectory, PlayerExecutableName(request.Target)),
                scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
                options = buildOptionsFlags,
            };

            // Addressablesはアクティブターゲット向けに焼かれるため、先にターゲットを合わせる
            // Addressables bakes for the active target, so switch the target before building content
            // 不一致のまま焼くと別APIのシェーダしか入らず、実機が全マゼンタになる
            // A mismatch bakes shaders for the wrong graphics API and the player renders everything magenta
            if (EditorUserBuildSettings.activeBuildTarget != request.Target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildPipeline.GetBuildTargetGroup(request.Target), request.Target))
            {
                Debug.LogError("Build target switch failed: " + request.Target);
                return PlayerBuildOutcome.PlayerBuildFailed;
            }

            // Addressablesコンテンツをクリーンビルドする
            // Clean build Addressables content before building the player
            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
            if (!string.IsNullOrEmpty(addressablesResult.Error))
            {
                Debug.LogError("Addressables Build Failed: " + addressablesResult.Error);
                return PlayerBuildOutcome.AddressablesBuildFailed;
            }
            Debug.Log("Addressables Build Succeeded: " + addressablesResult.OutputPath);

            var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
            Debug.Log("Build Result :" + report.summary.result);

            // 成功時のみ、動作に必要なCEFランタイムとゲームデータを同梱する
            // Only on success, bundle the CEF runtime and game data the player needs to run
            if (report.summary.result == BuildResult.Succeeded)
            {
                CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, request.IsStrictBundling);
                if (request.BundleLocalGameData) GameDataBundler.Bundle(request.OutputDirectory, request.IsStrictBundling);
            }

            Debug.Log("Build Output Path :" + report.summary.outputPath);
            Debug.Log("Build Summary TotalSize :" + report.summary.totalSize);
            Debug.Log("Build Finish Time : " + DateTime.Now);
            Debug.Log("Build Time : " + (DateTime.Now - buildStartTime).ToString(@"hh\:mm\:ss"));

            return report.summary.result == BuildResult.Succeeded
                ? PlayerBuildOutcome.Succeeded
                : PlayerBuildOutcome.PlayerBuildFailed;

            #region Internal

            string PlayerExecutableName(BuildTarget target)
            {
                // OSごとの配布実行ファイル名を明示する
                // Explicit per-OS distributable executable name
                switch (target)
                {
                    case BuildTarget.StandaloneWindows64: return "moorestech.exe";
                    case BuildTarget.StandaloneOSX: return "moorestech.app";
                    default: return "moorestech";
                }
            }

            #endregion
        }

        #region from Github Action

        public static void WindowsBuildFromGithubAction()
        {
            BuildFromGithubAction(BuildTarget.StandaloneWindows64);
        }

        public static void MacOsBuildFromGithubAction()
        {
            BuildFromGithubAction(BuildTarget.StandaloneOSX);
        }

        public static void LinuxBuildFromGithubAction()
        {
            BuildFromGithubAction(BuildTarget.StandaloneLinux64);
        }

        private static void BuildFromGithubAction(BuildTarget buildTarget)
        {
            // CI入口: 現行契約を維持（Output_<target>固定・警告のみの同梱・ゲームデータ無し）
            // CI entry keeps the current contract: fixed Output_<target>, warn-only bundling, no game data
            // Developmentで固定するのはメモリ効率のため（Release=CompressWithLz4は圧縮でバッチ機のメモリを食う）
            // Development is pinned for memory efficiency (Release CompressWithLz4 eats batch-machine memory)
            var outcome = Execute(new PlayerBuildRequest
            {
                Target = buildTarget,
                OutputDirectory = "Output_" + buildTarget,
                IsDevelopmentBuild = true,
                IsStrictBundling = false,
                BundleLocalGameData = false,
            });

            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }

        #endregion
    }
}
