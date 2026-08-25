using System;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入）
    // Event exhibition mode's enable flag and settings, injected through env vars by the launch script
    public readonly struct EventExhibitionSettings
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string EditorOptInEnvKey = "MOORESTECH_EVENT_MODE_EDITOR";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        private const int DefaultIdleTimeoutSeconds = 180;

        public readonly bool IsEnabled;
        public readonly int IdleTimeoutSeconds;

        private EventExhibitionSettings(bool isEnabled, int idleTimeoutSeconds)
        {
            IsEnabled = isEnabled;
            IdleTimeoutSeconds = idleTimeoutSeconds;
        }

        public static EventExhibitionSettings FromEnvironment()
        {
            return Parse(
                Environment.GetEnvironmentVariable(EnableEnvKey),
                Environment.GetEnvironmentVariable(IdleTimeoutEnvKey),
                Environment.GetEnvironmentVariable(EditorOptInEnvKey),
                UnityEngine.Application.isEditor);
        }

        // 有効値は"1"のみ、タイムアウトは正整数のみ受理し他は既定値へ落とす
        // Enable accepts "1" alone; the timeout accepts positive ints only and otherwise falls back to the default
        // Editorは開発機のワールドを不可逆に消すため、専用キーの明示opt-inが無い限り無効にする
        // The Editor wipes a developer's world irreversibly, so it stays off without the dedicated opt-in key
        public static EventExhibitionSettings Parse(string enableRawValue, string idleTimeoutRawValue, string editorOptInRawValue, bool isEditor)
        {
            var isEnabled = enableRawValue == "1" && (!isEditor || editorOptInRawValue == "1");
            var idleTimeoutSeconds = int.TryParse(idleTimeoutRawValue, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
            return new EventExhibitionSettings(isEnabled, idleTimeoutSeconds);
        }
    }
}
