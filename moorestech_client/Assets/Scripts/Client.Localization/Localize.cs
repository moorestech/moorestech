using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Master;
using Game.Context;
using Mod.Loader;
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

        private static readonly Dictionary<string, Dictionary<string, string>> mutableDictionaries = new();
        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> mergedDictionary = new();
        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var vanillaDictionaries = VanillaLocalizationDictionaryFactory.Create();
            mutableDictionaries.Clear();
            mergedDictionary.Clear();

            // 可変正本と外部向けread-only viewを同じinner辞書へ結び付ける
            // Bind the mutable source of truth and public read-only views to the same inner dictionaries
            foreach (var languageDictionary in vanillaDictionaries)
            {
                mutableDictionaries.Add(languageDictionary.Key, languageDictionary.Value);
                mergedDictionary.Add(
                    languageDictionary.Key,
                    new ReadOnlyDictionary<string, string>(languageDictionary.Value));
            }

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

        public static string GetContent(string derivedKey)
        {
            return LocalizationTextResolver.Resolve(mergedDictionary, currentLanguageCode, derivedKey);
        }

        public static void MergeGameDictionaries(ModsResource modsResource)
        {
            // DI登録済みコンテナからマスタと同じmod順を受け取る
            // Read the exact master mod order from the registered DI container
            var masterContainer = ServerContext.GetService<MasterJsonFileContainer>();
            MergeGameDictionaries(modsResource, masterContainer.SortedModIds);
        }

        internal static void MergeGameDictionaries(
            ModsResource modsResource,
            IReadOnlyList<ModId> orderedModIds)
        {
            var candidate = VanillaLocalizationDictionaryFactory.Create();
            ModLocalizationMerger.Merge(modsResource, orderedModIds, candidate);

            // マスタ原文はmod CSVのSourceより後に正本として重ねる
            // Overlay master source text as the source of truth after mod CSV Source values
            foreach (var sourceText in MasterSourceTextCollector.Collect())
            {
                if (string.IsNullOrEmpty(sourceText.Value)) continue;
                candidate[SourcePseudoLocale][sourceText.Key] = sourceText.Value;
            }

            // 全合成成功後に既存viewのinner辞書へ一括反映する
            // Commit into existing view-backed dictionaries only after composition fully succeeds
            foreach (var candidateLanguage in candidate)
            {
                var destination = mutableDictionaries[candidateLanguage.Key];
                destination.Clear();
                foreach (var text in candidateLanguage.Value)
                {
                    destination.Add(text.Key, text.Value);
                }
            }

            onLanguageChangedSubject.OnNext(Unit.Default);
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
            // 内部更新を反映し続けるread-only viewをWeb配信にも公開する
            // Expose a live read-only view that reflects internal dictionary updates to Web delivery
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
