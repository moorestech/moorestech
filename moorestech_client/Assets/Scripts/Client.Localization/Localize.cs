using System;
using System.Collections.Generic;
using System.Threading;
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

        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> publishedSnapshot;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var snapshot = VanillaLocalizationDictionaryFactory.CreateSnapshot();
            Volatile.Write(ref publishedSnapshot, snapshot);

            // 選択不能な保存値は生成済み英語辞書へ戻す
            // Fall back to the generated English dictionary for unselectable persisted values
            var savedLanguageCode = PlayerPrefs.GetString(LanguagePreferenceKey, DefaultLanguageCode);
            currentLanguageCode = snapshot.ContainsKey(savedLanguageCode) &&
                                  savedLanguageCode != SourcePseudoLocale
                ? savedLanguageCode
                : DefaultLanguageCode;
        }

        public static string Get(LocalizationKey key)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
        }

        public static string GetLegacy(string rawKey)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, rawKey);
        }

        public static string GetContent(string derivedKey)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, derivedKey);
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

            // 全合成成功後にfreeze済みsnapshot参照を一度だけ公開する
            // Publish the frozen snapshot reference once only after composition fully succeeds
            var snapshot = VanillaLocalizationDictionaryFactory.Freeze(candidate);
            Volatile.Write(ref publishedSnapshot, snapshot);
            onLanguageChangedSubject.OnNext(Unit.Default);
        }

        public static void SetLanguage(string languageCode)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);

            // Source以外を選択言語に限定
            // Allow selecting only non-Source locales
            if (languageCode != SourcePseudoLocale && snapshot.ContainsKey(languageCode))
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
            var snapshot = Volatile.Read(ref publishedSnapshot);

            // request中に変化しないsnapshotのread-only辞書をWeb配信へ公開する
            // Expose a read-only snapshot dictionary that stays stable throughout the request
            if (snapshot.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }

            dictionary = null;
            return false;
        }
    }
}
