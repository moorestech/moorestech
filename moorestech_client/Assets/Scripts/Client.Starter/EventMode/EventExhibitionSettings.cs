using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using UnityEngine;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入）
    // Event exhibition mode's enable flag and settings, injected through env vars by the launch script
    public readonly struct EventExhibitionSettings
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string EditorOptInEnvKey = "MOORESTECH_EVENT_MODE_EDITOR";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        private const string LanguageEnvKey = "MOORESTECH_EVENT_LANGUAGE";
        private const int DefaultIdleTimeoutSeconds = 180;

        public readonly bool IsEnabled;
        public readonly int IdleTimeoutSeconds;
        public readonly string LanguageCode;

        private EventExhibitionSettings(bool isEnabled, int idleTimeoutSeconds, string languageCode)
        {
            IsEnabled = isEnabled;
            IdleTimeoutSeconds = idleTimeoutSeconds;
            LanguageCode = languageCode;
        }

        public static EventExhibitionSettings FromEnvironment()
        {
            var languageRawValue = Environment.GetEnvironmentVariable(LanguageEnvKey);
            var settings = Parse(
                Environment.GetEnvironmentVariable(EnableEnvKey),
                Environment.GetEnvironmentVariable(IdleTimeoutEnvKey),
                Environment.GetEnvironmentVariable(EditorOptInEnvKey),
                Application.isEditor,
                languageRawValue,
                Localize.GetLanguageCodes());

            // 未知の言語コードはログだけ残し起動は止めない
            // An unknown language code only logs and never stops boot
            if (!string.IsNullOrEmpty(languageRawValue) && settings.LanguageCode != languageRawValue)
                Debug.LogError($"EventExhibitionSettings: unknown {LanguageEnvKey}={languageRawValue}, falling back to {settings.LanguageCode}");
            return settings;
        }

        // 有効値は"1"のみ、タイムアウトは正整数のみ受理し他は既定値へ落とす
        // Enable accepts "1" alone; the timeout accepts positive ints only and otherwise falls back to the default
        // Editorは開発機のワールドを不可逆に消すため、専用キーの明示opt-inが無い限り無効にする
        // The Editor wipes a developer's world irreversibly, so it stays off without the dedicated opt-in key
        // 言語は既知コードのみ受理し他はenglishへ落とす
        // The language accepts known codes only and otherwise falls back to english
        public static EventExhibitionSettings Parse(string enableRawValue, string idleTimeoutRawValue, string editorOptInRawValue, bool isEditor, string languageRawValue, IReadOnlyCollection<string> supportedLanguageCodes)
        {
            var isEnabled = enableRawValue == "1" && (!isEditor || editorOptInRawValue == "1");
            var idleTimeoutSeconds = int.TryParse(idleTimeoutRawValue, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
            var languageCode = !string.IsNullOrEmpty(languageRawValue) && supportedLanguageCodes.Contains(languageRawValue)
                ? languageRawValue
                : Localize.DefaultLanguageCode;
            return new EventExhibitionSettings(isEnabled, idleTimeoutSeconds, languageCode);
        }
    }
}
