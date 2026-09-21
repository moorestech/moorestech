#if UNITY_EDITOR
using Client.Game.InGame.BugReport.Playtest;
using Server.Boot;
using UnityEngine;

namespace Client.Starter.Editor
{
    /// <summary>
    /// 直Playでも完全なバグ報告を揃える（ADR 0066）。
    /// Makes direct-play bug reports complete (ADR 0066).
    /// </summary>
    public static class DirectPlayAlwaysOnCaptureSettings
    {
        public static void ApplyIfNeeded()
        {
            // 印は開始ゲートが後で消費するので、ここでは覗くだけにする
            // The start gates consume the mark later, so this only peeks at it
            ApplyForUnattendedReason(PlaytestStartGateBypass.PeekUnattendedReason());
        }

        // CIでも有人経路を検証するため、無人理由を受け取る
        // Takes the unattended reason so CI can verify the attended path
        internal static void ApplyForUnattendedReason(string unattendedReason)
        {
            // 無人起動は有効にしないだけで、無効へは上書きしない。記録したい無人テストは起動前に明示的に有効化している
            // An unattended boot is merely left alone, never forced to disabled: unattended tests that want capture enable it up front
            if (unattendedReason != null)
            {
                Debug.Log($"[DirectPlayAlwaysOnCaptureSettings] 無人起動のため直Playの常時記録を自動では有効にしません reason:{unattendedReason}");
                return;
            }

            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());
        }
    }
}
#endif
