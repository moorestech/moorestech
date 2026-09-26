using System;
using System.IO;
using System.Linq;
using Client.Editor.Build.Bundlers;
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

            // 同梱・検査の方針は用途からだけ導く
            // Bundling and check policy derive from the purpose alone
            var isStrictBundling = BuildPurposeRules.IsStrictBundling(request.Purpose);

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

            // CEFのMacランタイムがarm64のみのため、Macは焼く前にarm64へ固定する
            // CEF's Mac runtime is arm64 only, so pin the Mac player to arm64 before building
            if (request.Target == BuildTarget.StandaloneOSX && !MacPlayerArchitecture.TryPinAppleSilicon())
            {
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

            // 他の同梱と同じく strict を引数で渡して焼く。strict の関門は BuildPlayer の数十分より前に落とす
            // Bake with strict passed as an argument like the other bundlers; the strict gate fails before BuildPlayer's lengthy run
            BuildInfoWriter.Write(isStrictBundling, request.Target);
            var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
            Debug.Log("Build Result :" + report.summary.result);

            // 成功時のみ、動作に必要なCEFランタイムとゲームデータを同梱する
            // Only on success, bundle the CEF runtime and game data the player needs to run
            if (report.summary.result == BuildResult.Succeeded)
            {
                CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, isStrictBundling);
                FfmpegRuntimeBundler.Bundle(request.Target, report.summary.outputPath, isStrictBundling);
                if (BuildPurposeRules.BundlesLocalGameData(request.Purpose))
                {
                    GameDataBundler.Bundle(request.OutputDirectory, isStrictBundling);
                    WorldSnapshotBundler.Bundle(request.OutputDirectory, isStrictBundling);
                }

                // 展示会の起動ループは展示会ビルドにだけ入れる（Steam配布のMac版へ混ぜない）
                // The exhibition loop ships only with exhibition builds, never with the Steam Mac artifact
                if (BuildPurposeRules.BundlesExhibitionLaunchScript(request.Purpose))
                    EventLoopScriptBundler.Bundle(request.OutputDirectory, isStrictBundling);

                // 同梱で崩れた署名を最後にまとめて張り直す
                // Re-seal the signature broken by bundling, as the very last step
                if (request.Target == BuildTarget.StandaloneOSX)
                    MacAppAdHocSigner.Sign(report.summary.outputPath, isStrictBundling);
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
                Purpose = BuildPurpose.Ci,
                IsDevelopmentBuild = true,
            });

            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }

        #endregion
    }
}
