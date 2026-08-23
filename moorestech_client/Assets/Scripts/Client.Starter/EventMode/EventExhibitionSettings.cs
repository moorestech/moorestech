using System;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入）
    // Event exhibition mode's enable flag and settings, injected through env vars by the launch script
    public readonly struct EventExhibitionSettings
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
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
            return Parse(Environment.GetEnvironmentVariable(EnableEnvKey), Environment.GetEnvironmentVariable(IdleTimeoutEnvKey));
        }

        // 有効値は"1"のみ、タイムアウトは正整数のみ受理し他は既定値へ落とす
        // Enable accepts "1" alone; the timeout accepts positive ints only and otherwise falls back to the default
        public static EventExhibitionSettings Parse(string enableRawValue, string idleTimeoutRawValue)
        {
            var isEnabled = enableRawValue == "1";
            var idleTimeoutSeconds = int.TryParse(idleTimeoutRawValue, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
            return new EventExhibitionSettings(isEnabled, idleTimeoutSeconds);
        }
    }
}
