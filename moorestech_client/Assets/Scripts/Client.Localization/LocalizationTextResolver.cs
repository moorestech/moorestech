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
            return $"[!{key}]";

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
    }
}
