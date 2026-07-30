using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;

namespace Client.Localization
{
    public static class Localize
    {
        internal const string DefaultLanguageCode = "english";
        internal const string LanguagePreferenceKey = "LanguageCode";
        public const string SourcePseudoLocale = "source";

        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> mergedDictionary = new();
        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            mergedDictionary.Clear();

            // 空訳を除き埋め込み辞書を構築
            // Build embedded dictionaries without empty translations
            foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
            {
                VanillaLocalizationTable.TryGetLanguage(languageCode, out var table);
                var languageDictionary = new Dictionary<string, string>();
                foreach (var entry in table)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    languageDictionary.Add(entry.Key, entry.Value);
                }

                mergedDictionary.Add(
                    languageCode,
                    new ReadOnlyDictionary<string, string>(languageDictionary));
            }

            // Sourceも擬似localeとして配信
            // Deliver Source as a pseudo-locale
            var sourceDictionary = new Dictionary<string, string>();
            foreach (var entry in VanillaLocalizationTable.SourceTexts)
            {
                if (string.IsNullOrEmpty(entry.Value)) continue;
                sourceDictionary.Add(entry.Key, entry.Value);
            }

            mergedDictionary.Add(
                SourcePseudoLocale,
                new ReadOnlyDictionary<string, string>(sourceDictionary));

            // 選択不能な保存値は生成済み英語辞書へ戻す
            // Fall back to the generated English dictionary for unselectable persisted values
            var savedLanguageCode = PlayerPrefs.GetString(LanguagePreferenceKey, DefaultLanguageCode);
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
            return LocalizationTextResolver.Resolve(mergedDictionary, currentLanguageCode, rawKey);
        }

        public static void SetLanguage(string languageCode)
        {
            // Source以外を選択言語に限定
            // Allow selecting only non-Source locales
            if (languageCode != SourcePseudoLocale && mergedDictionary.ContainsKey(languageCode))
            {
                currentLanguageCode = languageCode;
                PlayerPrefs.SetString(LanguagePreferenceKey, languageCode);
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
