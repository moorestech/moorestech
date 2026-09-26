using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// Mac Playerをarm64専用に固定する（CEFのMacランタイムがosx-arm64しか無いため。ADR 0071）
    /// Pins the Mac player to arm64 only, since CEF's Mac runtime exists only for osx-arm64 (ADR 0071)
    /// </summary>
    internal static class MacPlayerArchitecture
    {
        public static bool TryPinAppleSilicon()
        {
#if UNITY_EDITOR_OSX
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Debug.Log("[MacPlayerArchitecture] macOS player architecture pinned to ARM64");
            return true;
#else
            // この設定の型はMacホストのEditorにだけある拡張アセンブリに属するため、他ホストからのMacビルドは理由を残して止める
            // The setting's type lives in an extension assembly shipped only with Mac-host Editors, so a Mac build from another host stops with a reason
            Debug.LogError("[MacPlayerArchitecture] macOS向けビルドはmacOSホストのEditorでしかarm64へ固定できないため中止します");
            return false;
#endif
        }
    }
}
