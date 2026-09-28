using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// arm64固定の結果（ホスト都合の不能とビルド失敗を呼び出し側が区別できるようにする）
    /// The pinning result, so callers can tell a host limitation apart from a build failure
    /// </summary>
    internal enum MacArchitecturePinResult
    {
        Pinned,
        UnsupportedHost,
    }

    /// <summary>
    /// Mac Playerをarm64専用に固定する（CEFのMacランタイムがosx-arm64しか無いため。ADR 0071）
    /// Pins the Mac player to arm64 only, since CEF's Mac runtime exists only for osx-arm64 (ADR 0071)
    /// </summary>
    internal static class MacPlayerArchitecture
    {
        public const string UnsupportedHostReason =
            "[MacPlayerArchitecture] 非Macホストではarm64へ固定できず、osx-arm64専用CEFのWeb UIが動かない可能性があります";

#if UNITY_EDITOR_OSX
        private static UnityEditor.Build.OSArchitecture _architectureBeforePin;
        private static bool _isPinned;
#endif

        public static MacArchitecturePinResult PinAppleSilicon()
        {
#if UNITY_EDITOR_OSX
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
                _architectureBeforePin = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
                _isPinned = true;
            }
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Debug.Log("[MacPlayerArchitecture] macOS player architecture pinned to ARM64");
            return MacArchitecturePinResult.Pinned;
#else
            // 設定型はMacホスト専用
            // The setting type exists only on Mac hosts
            return MacArchitecturePinResult.UnsupportedHost;
#endif
        }

        public static void RestoreArchitectureBeforePin()
        {
#if UNITY_EDITOR_OSX
            if (!_isPinned) return;

            UnityEditor.OSXStandalone.UserBuildSettings.architecture = _architectureBeforePin;
            _isPinned = false;
            Debug.Log("[MacPlayerArchitecture] macOS player architecture restored");
#endif
        }
    }
}
