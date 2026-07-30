using System;
using System.Collections.Generic;

namespace Client.Localization
{
    internal static class LocalizationTextResolver
    {
        public static string Resolve(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictionaries,
            string currentLanguageCode,
            string key)
        {
            // 対象言語から英語、Sourceの順に空でない文言を解決する
            // Resolve non-empty text from target, English, then Source in order
            if (TryGetText(dictionaries[currentLanguageCode], key, out var currentText)) return currentText;
            if (TryGetText(dictionaries[Localize.DefaultLanguageCode], key, out var englishText)) return englishText;
            if (TryGetText(dictionaries[Localize.SourcePseudoLocale], key, out var sourceText)) return sourceText;
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
