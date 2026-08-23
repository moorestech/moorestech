using System;

namespace Client.Starter.EventMode
{
    // 有効判定と設定値（起動スクリプトが注入）
    // Enable flag and settings, injected by the launch script
    public static class EventExhibitionMode
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        private const int DefaultIdleTimeoutSeconds = 180;

        internal static bool IsEnabled => IsEnabledValue(Environment.GetEnvironmentVariable(EnableEnvKey));
        internal static int IdleTimeoutSeconds => ParseIdleTimeoutSeconds(Environment.GetEnvironmentVariable(IdleTimeoutEnvKey));

        public static bool IsEnabledValue(string rawValue)
        {
            return rawValue == "1";
        }

        public static int ParseIdleTimeoutSeconds(string rawValue)
        {
            // 正整数のみ受理、他は既定値
            // Accept positive int only, else default
            return int.TryParse(rawValue, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
        }
    }
}
