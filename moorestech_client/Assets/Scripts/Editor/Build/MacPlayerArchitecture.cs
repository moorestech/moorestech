using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Mac Playerをarm64専用に固定する（CEFのMacランタイムがosx-arm64しか無いため。ADR 0071）
    /// Pins the Mac player to arm64 only, since CEF's Mac runtime exists only for osx-arm64 (ADR 0071)
    /// </summary>
    internal static class MacPlayerArchitecture
    {
        public static bool TryPinAppleSilicon(bool isStrict)
        {
#if UNITY_EDITOR_OSX
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Debug.Log("[MacPlayerArchitecture] macOS player architecture pinned to ARM64");
            return true;
#else
            // 設定型はMacホスト専用。strictは停止し、他はCEF制約を警告して続行する
            // The setting exists only on Mac hosts; strict stops, others warn about CEF and continue
            const string reason = "[MacPlayerArchitecture] 非Macホストではarm64へ固定できず、osx-arm64専用CEFのWeb UIが動かない可能性があります";
            if (isStrict)
            {
                Debug.LogError(reason);
                return false;
            }
            Debug.LogWarning(reason);
            return true;
#endif
        }
    }
}
