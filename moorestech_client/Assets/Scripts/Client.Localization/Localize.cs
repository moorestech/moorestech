using System;
using System.Collections.Generic;
using System.Threading;
using Core.Master;
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

        // 原文は言語辞書ではないため、mod CSVの予約列名とHTTP経路名としてだけ使う
        // Source is not a language dictionary, so this name only serves the reserved mod CSV column and HTTP route
        public const string SourcePseudoLocale = "source";

        private static readonly Subject<Unit> onLanguageChangedSubject = new();
        public static readonly IObservable<Unit> OnLanguageChanged = onLanguageChangedSubject;
        private static PublishedLocalizationDictionarySnapshot publishedSnapshot;
        private static long dictionaryRevision;
        private static string currentLanguageCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            PublishSnapshot(VanillaLocalizationDictionaryFactory.Create());

            // 選択不能な保存値は生成済み英語辞書へ戻す
            // Fall back to the generated English dictionary for unselectable persisted values
            var savedLanguageCode = PlayerPrefs.GetString(LanguagePreferenceKey, DefaultLanguageCode);
            var languages = Volatile.Read(ref publishedSnapshot).Languages;
            currentLanguageCode = languages.ContainsKey(savedLanguageCode)
                ? savedLanguageCode
                : DefaultLanguageCode;
        }

        public static string Get(LocalizationKey key)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
        }

        // TextMeshProLocalizeのInspector入力キー専用のレガシー経路（型付きキーはGet/GetContent）
        // Legacy path used only by TextMeshProLocalize's Inspector keys; typed keys use Get/GetContent
        public static string GetLegacy(string rawKey)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, rawKey);
        }

        public static string GetContent(ContentLocalizationKey key)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
        }

        public static string GetFormatted(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            return LocalizationTextInterpolator.Interpolate(Get(key), textParams);
        }

        // mod順とMaster原文は呼び出し側が決め、基盤は辞書だけを合成する
        // Callers decide mod order and Master sources; the foundation only composes dictionaries
        public static void MergeGameDictionaries(
            ModsResource modsResource,
            IReadOnlyList<ModId> orderedModIds,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            var candidate = VanillaLocalizationDictionaryFactory.Create();
            ModLocalizationMerger.Merge(modsResource, orderedModIds, candidate);

            // mod Sourceの後へMaster正本を重ね、空原文も欠落として確定する
            // Overlay canonical Master after mod Source and finalize empty sources as omissions
            OverlayMasterSourceTexts(candidate, masterSourceTexts);

            // 全合成成功後にfreeze済みsnapshot参照を一度だけ公開する
            // Publish the frozen snapshot reference once only after composition fully succeeds
            PublishSnapshot(candidate);
            onLanguageChangedSubject.OnNext(Unit.Default);
        }

        internal static void OverlayMasterSourceTexts(
            LocalizationDictionaryCandidate candidate,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            foreach (var sourceText in masterSourceTexts)
            {
                // 空Masterはmod由来Sourceを残さずcanonical欠落にする
                // Empty Master removes mod Source so the canonical value remains missing
                if (string.IsNullOrEmpty(sourceText.Value))
                {
                    candidate.SourceTexts.Remove(sourceText.Key);
                    continue;
                }

                candidate.SourceTexts[sourceText.Key] = sourceText.Value;
            }
        }

        public static bool TrySetLanguage(string languageCode)
        {
            // 可否は戻り値だけで表す（外部入力ハンドラがActionResultへ変換する）
            // Success/failure is expressed only via the return value; handlers map it to ActionResult
            if (string.IsNullOrEmpty(languageCode)) return false;

            // 公開snapshotの実言語だけを判定基準にする
            // Judge only against the real languages carried by the published snapshot
            var languages = Volatile.Read(ref publishedSnapshot).Languages;
            if (!languages.ContainsKey(languageCode)) return false;

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
            return snapshot.Languages.TryGetValue(languageCode, out dictionary);
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
                snapshot.Languages.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }

            dictionary = null;
            return false;
        }

        public static bool TryGetSourceTexts(
            long expectedRevision,
            out IReadOnlyDictionary<string, string> sourceTexts)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);

            // 原文も同じsnapshotでrevisionを検証し、実言語と同じ世代保証で配信する
            // Source texts validate the revision on the same snapshot for the same generation guarantee
            if (snapshot.Revision == expectedRevision)
            {
                sourceTexts = snapshot.SourceTexts;
                return true;
            }

            sourceTexts = null;
            return false;
        }

        private static void PublishSnapshot(LocalizationDictionaryCandidate candidate)
        {
            var revision = Interlocked.Increment(ref dictionaryRevision);
            Volatile.Write(
                ref publishedSnapshot,
                VanillaLocalizationDictionaryFactory.Freeze(candidate, revision));
        }
    }
}
