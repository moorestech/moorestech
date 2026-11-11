using System;
using System.Collections.Generic;

namespace Client.Localization
{
    internal static class LocalizationTextResolver
    {
        public static string Resolve(
            PublishedLocalizationDictionarySnapshot snapshot,
            string currentLanguageCode,
            string key)
        {
            // 対象言語から英語、Sourceの順に空でない文言を解決する
            // Resolve non-empty text from target, English, then Source in order
            if (TryGetText(snapshot.Languages[currentLanguageCode], key, out var currentText)) return currentText;
            if (TryGetText(snapshot.Languages[Localize.DefaultLanguageCode], key, out var englishText)) return englishText;
            if (TryGetText(snapshot.SourceTexts, key, out var sourceText)) return sourceText;
            return MissingMark(key);

            #region Internal

            bool TryGetText(
                IReadOnlyDictionary<string, string> dictionary,
                string textKey,
                out string text)
            {
                if (dictionary.TryGetValue(textKey, out text) && !string.IsNullOrEmpty(text)) return true;
                text = null;
                return false;
            }

            #endregion
        }

        // 欠けの印の書式を書く唯一の場所（Web の i18nStore も同じ「[!key]」）。読む側は Localize.IsMissingText を使う
        // The only place that writes the missing-marker format (same "[!key]" as the Web i18nStore); readers use Localize.IsMissingText
        internal static string MissingMark(string key)
        {
            return $"[!{key}]";
        }
    }
}
