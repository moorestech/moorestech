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
        public const string DefaultLanguageCode = "english";
        internal const string LanguagePreferenceKey = "LanguageCode";
        public const string SourcePseudoLocale = "source";

        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static PublishedLocalizationDictionarySnapshot publishedSnapshot;
        private static long dictionaryRevision;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var snapshot = VanillaLocalizationDictionaryFactory.CreateSnapshot();
            PublishSnapshot(snapshot);

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
            var snapshot = Volatile.Read(ref publishedSnapshot).Dictionaries;
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
        }

        // Inspector入力の生キーだけを解決するレガシー経路（導出キーはGetContent）
        // Legacy path resolving only Inspector-authored raw keys; derived keys use GetContent
        public static string GetLegacy(string rawKey)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot).Dictionaries;
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, rawKey);
        }

        public static string GetContent(ContentLocalizationKey key)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot).Dictionaries;
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
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

            // mod Sourceの後へMaster正本を重ね、空原文も欠落として確定する
            // Overlay canonical Master after mod Source and finalize empty sources as omissions
            OverlayMasterSourceTexts(candidate, MasterSourceTextCollector.Collect());

            // 全合成成功後にfreeze済みsnapshot参照を一度だけ公開する
            // Publish the frozen snapshot reference once only after composition fully succeeds
            var snapshot = VanillaLocalizationDictionaryFactory.Freeze(candidate);
            PublishSnapshot(snapshot);
            onLanguageChangedSubject.OnNext(Unit.Default);
        }

        internal static void OverlayMasterSourceTexts(
            Dictionary<string, Dictionary<string, string>> dictionaries,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            var sourceDictionary = dictionaries[SourcePseudoLocale];
            foreach (var sourceText in masterSourceTexts)
            {
                // 空Masterはmod由来Sourceを残さずcanonical欠落にする
                // Empty Master removes mod Source so the canonical value remains missing
                if (string.IsNullOrEmpty(sourceText.Value))
                {
                    sourceDictionary.Remove(sourceText.Key);
                    continue;
                }

                sourceDictionary[sourceText.Key] = sourceText.Value;
            }
        }

        public static bool TrySetLanguage(string languageCode)
        {
            // 可否は戻り値だけで表す（外部入力ハンドラがActionResultへ変換する）
            // Success/failure is expressed only via the return value; handlers map it to ActionResult
            if (string.IsNullOrEmpty(languageCode)) return false;

            // 公開snapshotを唯一の判定基準とし、Source擬似localeは選択させない
            // Judge only against the published snapshot and never allow the Source pseudo-locale
            var snapshot = Volatile.Read(ref publishedSnapshot).Dictionaries;
            if (languageCode == SourcePseudoLocale || !snapshot.ContainsKey(languageCode)) return false;

            currentLanguageCode = languageCode;
            PlayerPrefs.SetString(LanguagePreferenceKey, languageCode);
            PlayerPrefs.Save();
            onLanguageChangedSubject.OnNext(Unit.Default);
            return true;
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

        public static long GetDictionaryRevision()
        {
            return Volatile.Read(ref publishedSnapshot).Revision;
        }

        public static bool TryGetDictionary(
            string languageCode,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return snapshot.Dictionaries.TryGetValue(languageCode, out dictionary);
        }

        public static bool TryGetDictionary(
            string languageCode,
            long expectedRevision,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);

            // revisionと辞書を同じsnapshotから検証し、HTTP応答の異世代混在を防ぐ
            // Validate revision and dictionary from one snapshot to prevent mixed HTTP generations
            if (snapshot.Revision == expectedRevision &&
                snapshot.Dictionaries.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }

            dictionary = null;
            return false;
        }

        private static void PublishSnapshot(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictionaries)
        {
            var revision = Interlocked.Increment(ref dictionaryRevision);
            Volatile.Write(
                ref publishedSnapshot,
                new PublishedLocalizationDictionarySnapshot(revision, dictionaries));
        }
    }
}
