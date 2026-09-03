using System;
using UnityEngine;

namespace Client.Starter.EventMode
{
    // 出展モードの有効判定と設定値
    // Exhibition mode enable decision and settings
    public readonly struct EventExhibitionSettings
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string EditorOptInEnvKey = "MOORESTECH_EVENT_MODE_EDITOR";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";

        // ログとテストで同じキー名を参照する
        // Logs and tests reference the same key name
        internal const string LanguageEnvKey = "MOORESTECH_EVENT_LANGUAGE";
        private const int DefaultIdleTimeoutSeconds = 180;

        public readonly bool IsEnabled;
        public readonly int IdleTimeoutSeconds;

        // 要求された言語コードの生値。可否はLocalize側が適用時に判定する
        // Raw requested language code; Localize decides acceptance at apply time
        public readonly string RequestedLanguageCode;

        private EventExhibitionSettings(bool isEnabled, int idleTimeoutSeconds, string requestedLanguageCode)
        {
            IsEnabled = isEnabled;
            IdleTimeoutSeconds = idleTimeoutSeconds;
            RequestedLanguageCode = requestedLanguageCode;
        }

        public static EventExhibitionSettings FromEnvironment()
        {
            var raw = new EventModeEnvironmentValues
            {
                Enable = Environment.GetEnvironmentVariable(EnableEnvKey),
                IdleTimeoutSeconds = Environment.GetEnvironmentVariable(IdleTimeoutEnvKey),
                EditorOptIn = Environment.GetEnvironmentVariable(EditorOptInEnvKey),
                Language = Environment.GetEnvironmentVariable(LanguageEnvKey),
            };
            return Parse(raw, Application.isEditor);
        }

        // 有効値は"1"のみ、タイムアウトは正整数のみ受理し他は既定値へ落とす
        // Enable accepts "1" alone; the timeout accepts positive ints only and otherwise falls back to the default
        // Editorは開発機のワールドを不可逆に消すため、専用キーの明示opt-inが無い限り無効にする
        // The Editor wipes a developer's world irreversibly, so it stays off without the dedicated opt-in key
        public static EventExhibitionSettings Parse(EventModeEnvironmentValues raw, bool isEditor)
        {
            var isEnabled = raw.Enable == "1" && (!isEditor || raw.EditorOptIn == "1");
            var idleTimeoutSeconds = int.TryParse(raw.IdleTimeoutSeconds, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
            return new EventExhibitionSettings(isEnabled, idleTimeoutSeconds, raw.Language);
        }
    }
}
