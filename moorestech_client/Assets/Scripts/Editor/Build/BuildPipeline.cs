using System;
using System.IO;
using System.Linq;
using Client.Build.Policy;
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
            var policy = BuildPurposeRules.Resolve(request.Purpose, request.LocalDevelopmentChoosesDevelopment);

            var buildOptions = new BuildPlayerOptions
            {
                target = request.Target,
                locationPathName = Path.Combine(request.OutputDirectory, PlayerExecutableName(request.Target)),
                scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
                options = policy.IsDevelopmentBuild ? BuildOptions.Development : BuildOptions.CompressWithLz4,
            };

            // Addressablesはアクティブターゲット向けに焼かれるため、先にターゲットを合わせる
            // Addressables bakes for the active target, so switch the target before building content
            // 不一致のまま焼くと別APIのシェーダしか入らず、実機が全マゼンタになる
            // A mismatch bakes shaders for the wrong graphics API and the player renders everything magenta
            if (EditorUserBuildSettings.activeBuildTarget != request.Target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildPipeline.GetBuildTargetGroup(request.Target), request.Target))
            {
                Debug.LogError("Build target switch failed: " + request.Target);
                return PlayerBuildOutcome.BuildTargetSwitchFailed;
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
            BuildInfoWriter.Write(policy.IsStrictBundling, request.Target);

            // CEFのMacランタイムがarm64のみのため、配布用途のMacは焼く直前にarm64へ固定する
            // CEF's Mac runtime is arm64 only, so distribution Mac builds pin arm64 right before baking
            // Editor全体の設定を書き換えるため、復元を飛ばす離脱点を間に挟まない位置に置く
            // It rewrites an Editor-wide setting, so nothing that could exit early sits between the pin and the restore
            var pinsAppleSilicon = policy.PinsAppleSilicon && request.Target == BuildTarget.StandaloneOSX;
            if (pinsAppleSilicon && MacPlayerArchitecture.PinAppleSilicon() == MacArchitecturePinResult.UnsupportedHost)
            {
                Debug.LogError(MacPlayerArchitecture.UnsupportedHostReason);
                return PlayerBuildOutcome.MacArchitecturePinFailed;
            }

            var report = UnityEditor.BuildPipeline.BuildPlayer(buildOptions);
            Debug.Log("Build Result :" + report.summary.result);

            // 焼き終えた直後に戻す
            // Restore it as soon as the bake is done
            if (pinsAppleSilicon) MacPlayerArchitecture.RestoreArchitectureBeforePin();

            Debug.Log("Build Output Path :" + report.summary.outputPath);
            Debug.Log("Build Summary TotalSize :" + report.summary.totalSize);
            Debug.Log("Build Finish Time : " + DateTime.Now);
            Debug.Log("Build Time : " + (DateTime.Now - buildStartTime).ToString(@"hh\:mm\:ss"));

            if (report.summary.result != BuildResult.Succeeded) return PlayerBuildOutcome.PlayerBuildFailed;

            // 動作に必要なCEFランタイムとゲームデータを同梱する
            // Bundle the CEF runtime and game data the player needs to run
            CefRuntimeBundler.Bundle(request.Target, report.summary.outputPath, policy.IsStrictBundling);
            FfmpegRuntimeBundler.Bundle(request.Target, report.summary.outputPath, policy.IsStrictBundling);
            if (policy.BundlesLocalGameData)
            {
                GameDataBundler.Bundle(request.OutputDirectory, policy.IsStrictBundling);
                WorldSnapshotBundler.Bundle(request.OutputDirectory, policy.IsStrictBundling);
            }

            // 展示会限定（Steam版への混入防止）
            // Exhibition only; keep it out of Steam builds
            if (policy.BundlesExhibitionLaunchScript)
                EventLoopScriptBundler.Bundle(request.Target, request.OutputDirectory, policy.IsStrictBundling);

            // 同梱で崩れた署名を最後にまとめて張り直す
            // Re-seal the signature broken by bundling, as the very last step
            var reSignsMacApp = policy.ReSignsMacApp && request.Target == BuildTarget.StandaloneOSX;
            if (reSignsMacApp && !MacAppAdHocSigner.Sign(report.summary.outputPath))
                return PlayerBuildOutcome.MacSigningFailed;

            // 非配布用途のMacはarm64固定も再署名も省く。無音で縮退させず理由を残す
            // Non-distribution Mac builds skip both pinning and re-signing; say so instead of degrading silently
            if (request.Target == BuildTarget.StandaloneOSX && !policy.PinsAppleSilicon)
                Debug.Log($"[BuildPipeline] {request.Purpose} は配布用途でないためarm64固定とad-hoc再署名を行いません。osx-arm64専用CEFのWeb UIが動かない可能性があります");

            return PlayerBuildOutcome.Succeeded;

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
            var outcome = Execute(PlayerBuildRequest.ForCi(buildTarget, "Output_" + buildTarget));

            EditorApplication.Exit(outcome == PlayerBuildOutcome.Succeeded ? 0 : 1);
        }

        #endregion
    }
}
