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
        internal static PlayerBuildOutcome Execute(PlayerBuildRequest request)
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

                // 展示会の起動ループはmacの.commandなので、mac向けのローカル配布成果物にだけ入れる
                // The exhibition loop is a mac .command, so it ships only with mac local-distribution artifacts
                if (request.BundleLocalGameData && request.Target == BuildTarget.StandaloneOSX)
                    EventLoopScriptBundler.Bundle(request.OutputDirectory, request.IsStrictBundling);
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
