using UnityEngine;

namespace Client.Game.InGame.BugReport.Recording
{
    // 録画リングの起動可否。StartServerSettings.CaptureRingと同じ役割をclient側で担う
    // Whether the recording ring may start; plays the same role client-side as StartServerSettings.CaptureRing
    public static class BugReportRecordingSettings
    {
        public static bool Enabled { get; private set; } = true;

        public const string DisabledReason = "テスト・プレイテスト経路のため録画リングを無効化しています";

        // テスト・プレイテスト・QA起動経路が呼ぶ。プロセス寿命全体に効く起動時設定のため静的に持つ
        // Called by test/playtest/QA boot paths; process-lifetime boot setting, hence static
        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (!enabled) Debug.Log($"[BugReportRecordingSettings] {DisabledReason}");
        }
    }
}
