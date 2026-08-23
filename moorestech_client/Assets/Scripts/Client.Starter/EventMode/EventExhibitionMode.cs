using System;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入する）
    // Event exhibition mode flags and settings, injected via env vars by the launch script
    public static class EventExhibitionMode
    {
        public const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        public const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        public const int DefaultIdleTimeoutSeconds = 180;

        public static bool IsEnabled => IsEnabledValue(Environment.GetEnvironmentVariable(EnableEnvKey));
        public static int IdleTimeoutSeconds => ParseIdleTimeoutSeconds(Environment.GetEnvironmentVariable(IdleTimeoutEnvKey));

        public static bool IsEnabledValue(string rawValue)
        {
            return rawValue == "1";
        }

        public static int ParseIdleTimeoutSeconds(string rawValue)
        {
            // 正の整数のみ受理し、それ以外は既定値へ戻す / Accept only positive integers, else fall back to the default
            return int.TryParse(rawValue, out var seconds) && seconds > 0 ? seconds : DefaultIdleTimeoutSeconds;
        }
    }
}
