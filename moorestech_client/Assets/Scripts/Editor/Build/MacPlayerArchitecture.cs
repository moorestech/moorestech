using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// arm64固定の結果（Mac Build Support の欠落とビルド失敗を呼び出し側が区別できるようにする）
    /// The pinning result, so callers can tell a missing Mac Build Support apart from a build failure
    /// </summary>
    internal enum MacArchitecturePinResult
    {
        Pinned,
        MacBuildSupportMissing,
    }

    /// <summary>
    /// Mac Playerをarm64専用に固定する（Mac版はApple Silicon専用。同梱ffmpegもmacos-arm64版だけ。ADR 0071）
    /// Pins the Mac player to arm64 only; the Mac build is Apple Silicon only and bundles a macos-arm64 ffmpeg (ADR 0071)
    /// OSXStandalone.UserBuildSettings.architecture と同じ保存先を文字列APIで触る。型はMacホストでしかコンパイルに入らないが、設定はMac Build Supportがあればどのホストでも効く
    /// It writes the same storage as OSXStandalone.UserBuildSettings.architecture through the string API; the type compiles only on Mac hosts, but the setting works on any host that has Mac Build Support
    /// </summary>
    internal static class MacPlayerArchitecture
    {
        public const string MacBuildSupportMissingReason =
            "[MacPlayerArchitecture] このEditorにMac Build Supportが入っていないため、Mac Playerをarm64へ固定できません（Unity HubでMac Build Supportを追加してください）";

        // UserBuildSettings.architecture が読み書きする保存先（値は OSArchitecture の名前）
        // The storage UserBuildSettings.architecture reads and writes; the value is an OSArchitecture name
        private const string ArchitectureSettingName = "Architecture";

        private static string _architectureBeforePin;
        private static bool _isPinned;

        private static string MacPlatformName => UnityEditor.BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);

        public static MacArchitecturePinResult PinAppleSilicon()
        {
            // 設定の保存先はMac Build Supportが無いと効かないので、モジュールの有無で止める
            // The setting has no effect without Mac Build Support, so gate on the module being installed
            if (!UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                return MacArchitecturePinResult.MacBuildSupportMissing;
            }

            // 復元されないまま再固定すると元の値を失うため、上書きせず漏れを表明する
            // Re-pinning without a restore would lose the original value, so keep it and report the leak
            if (_isPinned)
            {
                Debug.LogError("[MacPlayerArchitecture] 前回の固定が復元されないまま再固定されました。元のアーキテクチャ設定は保持したままにします");
            }
            else
            {
                // Editor設定を書き換えるため、他のビルドへ持ち越さないよう元の値を控える
                // This rewrites an Editor-wide setting, so keep the old value and never leak it into other builds
                _architectureBeforePin = EditorUserBuildSettings.GetPlatformSettings(MacPlatformName, ArchitectureSettingName);
                _isPinned = true;
            }
            EditorUserBuildSettings.SetPlatformSettings(MacPlatformName, ArchitectureSettingName, OSArchitecture.ARM64.ToString());
            Debug.Log($"[MacPlayerArchitecture] macOS player architecture pinned to {EditorUserBuildSettings.GetPlatformSettings(MacPlatformName, ArchitectureSettingName)}");
            return MacArchitecturePinResult.Pinned;
        }

        public static void RestoreArchitectureBeforePin()
        {
            if (!_isPinned) return;

            EditorUserBuildSettings.SetPlatformSettings(MacPlatformName, ArchitectureSettingName, _architectureBeforePin);
            _isPinned = false;
            Debug.Log($"[MacPlayerArchitecture] macOS player architecture restored to '{_architectureBeforePin}'");
        }
    }
}
