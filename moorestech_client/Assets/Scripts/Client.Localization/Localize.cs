using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;

namespace Client.Localization
{
    public static class Localize
    {
        private const string DefaultLanguageCode = "english";
        public const string SourcePseudoLocale = "source";

        private static readonly Dictionary<string, Dictionary<string, string>> mergedDictionary = new();
        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            mergedDictionary.Clear();

            // 埋め込みテーブルから空文字を除いた言語辞書を構築する
            // Build language dictionaries from non-empty embedded table entries
            foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
            {
                VanillaLocalizationTable.TryGetLanguage(languageCode, out var table);
                var languageDictionary = new Dictionary<string, string>();
                foreach (var entry in table)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    languageDictionary.Add(entry.Key, entry.Value);
                }

                mergedDictionary.Add(languageCode, languageDictionary);
            }

            // Source列も同じ配信用辞書へ擬似ロケールとして追加する
            // Add the Source column to the shared delivery map as a pseudo-locale
            var sourceDictionary = new Dictionary<string, string>();
            foreach (var entry in VanillaLocalizationTable.SourceTexts)
            {
                if (string.IsNullOrEmpty(entry.Value)) continue;
                sourceDictionary.Add(entry.Key, entry.Value);
            }

            mergedDictionary.Add(SourcePseudoLocale, sourceDictionary);

            // 選択不能な保存値は生成済み英語辞書へ戻す
            // Fall back to the generated English dictionary for unselectable persisted values
            var savedLanguageCode = PlayerPrefs.GetString("LanguageCode", DefaultLanguageCode);
            currentLanguageCode = mergedDictionary.ContainsKey(savedLanguageCode) &&
                                  savedLanguageCode != SourcePseudoLocale
                ? savedLanguageCode
                : DefaultLanguageCode;
        }

        public static string Get(LocalizationKey key)
        {
            return GetLegacy(key.Key);
        }

        public static string GetLegacy(string rawKey)
        {
            // 対象言語から英語、Sourceの順に空でない文言を解決する
            // Resolve non-empty text from target, English, then Source in order
            if (mergedDictionary[currentLanguageCode].TryGetValue(rawKey, out var value)) return value;
            if (mergedDictionary[DefaultLanguageCode].TryGetValue(rawKey, out var english)) return english;
            if (mergedDictionary[SourcePseudoLocale].TryGetValue(rawKey, out var source)) return source;
            return $"[!{rawKey}]";
        }

        public static void SetLanguage(string languageCode)
        {
            // Source擬似ロケールを除く生成済み言語だけを選択可能にする
            // Allow selection of generated languages except the Source pseudo-locale
            if (languageCode != SourcePseudoLocale && mergedDictionary.ContainsKey(languageCode))
            {
                currentLanguageCode = languageCode;
                PlayerPrefs.SetString("LanguageCode", languageCode);
                PlayerPrefs.Save();
                onLanguageChangedSubject.OnNext(Unit.Default);
                return;
            }

            Debug.LogError($"[Localize] Language Code : {languageCode} is not found");
        }

        public static string GetCurrentLanguageCode()
        {
            return currentLanguageCode;
        }

        public static List<string> GetLanguageCodes()
        {
            var languageCodes = new List<string>();
            foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
            {
                languageCodes.Add(languageCode);
            }

            return languageCodes;
        }

        public static bool TryGetDictionary(
            string languageCode,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            // 初期化後は不変の正本をWeb配信にも公開する
            // Expose the immutable post-initialization source of truth to Web delivery
            if (mergedDictionary.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }

            dictionary = null;
            return false;
        }
    }
}
