#if UNITY_EDITOR
using Client.Game.InGame.BugReport.Playtest;
using Server.Boot;
using UnityEngine;

namespace Client.Starter.Editor
{
    /// <summary>
    /// エディタの直Playで常時記録を有効にする。メインメニューを通らない起動でもバグ報告の中身が揃うようにする（ADR 0066）。
    /// Enables always-on capture for an Editor direct play, so a boot that skips the main menu still yields a complete bug report (ADR 0066).
    /// </summary>
    public static class DirectPlayAlwaysOnCaptureSettings
    {
        public static void ApplyIfNeeded()
        {
            // 印は開始ゲートが後で消費するので、ここでは覗くだけにする
            // The start gates consume the mark later, so this only peeks at it
            ApplyForUnattendedReason(PlaytestStartGateBypass.PeekUnattendedReason());
        }

        // 無人の理由を引数で受ける本体。CIは常にbatchModeで無人になるため、有人の経路も検証できるよう分ける
        // The body takes the unattended reason, so the attended path stays verifiable under CI's always-unattended batch mode
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
