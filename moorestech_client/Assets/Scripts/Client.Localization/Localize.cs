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
            currentLanguageCode = IsSelectable(savedLanguageCode)
                ? savedLanguageCode
                : DefaultLanguageCode;
            if (savedLanguageCode != currentLanguageCode && PlayerPrefs.HasKey(LanguagePreferenceKey))
                Debug.LogWarning($"[Localize] saved language {savedLanguageCode} is unavailable; using {DefaultLanguageCode}");
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

        // mod順とMaster原文は呼び出し側が決め、基盤は辞書だけを合成する
        // Callers decide mod order and Master sources; the foundation only composes dictionaries
        public static void MergeGameDictionaries(
            ModsResource modsResource,
            IReadOnlyList<ModId> orderedModIds,
            IReadOnlyDictionary<string, string> masterSourceTexts)
        {
            var candidate = GameLocalizationDictionaryComposer.Compose(
                modsResource, orderedModIds, masterSourceTexts);

            // 全合成成功後にfreeze済みsnapshot参照を一度だけ公開する
            // Publish the frozen snapshot reference once only after composition fully succeeds
            PublishSnapshot(candidate);
            onLanguageChangedSubject.OnNext(Unit.Default);
        }

        public static bool TrySetLanguage(string languageCode)
        {
            return TryApplyLanguage(languageCode, true);
        }

        // 選択済みなら外部の言語ソースを読まない
        // Do not consult an external language source after the player has chosen
        public static bool TryApplyUnchosenLanguage(IUnchosenLanguageSource source)
        {
            if (PlayerPrefs.HasKey(LanguagePreferenceKey) &&
                IsSelectable(PlayerPrefs.GetString(LanguagePreferenceKey)))
            {
                Debug.Log("[Localize] temporary language rejected: the player already chose a language");
                return false;
            }
            if (source == null)
            {
                Debug.LogWarning("[Localize] temporary language rejected: source is null");
                return false;
            }
            if (!source.TryResolveGameLanguage(out var languageCode, out var failureReason))
            {
                Debug.LogWarning($"[Localize] temporary language unavailable: {failureReason}");
                return false;
            }
            return TryApplyLanguage(languageCode, false);
        }

        public static string GetCurrentLanguageCode()
        {
            return currentLanguageCode;
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

        private static bool IsSelectable(string languageCode)
        {
            return !string.IsNullOrEmpty(languageCode) &&
                   Volatile.Read(ref publishedSnapshot).Languages.ContainsKey(languageCode);
        }

        private static bool TryApplyLanguage(string languageCode, bool persist)
        {
            if (!IsSelectable(languageCode))
            {
                Debug.LogWarning($"[Localize] language rejected: unsupported code {languageCode ?? "<null>"}");
                return false;
            }

            // 保存は明示選択に限り、一時適用も同じイベントで通知する
            // Persist only explicit choices and notify through the same event for temporary application
            currentLanguageCode = languageCode;
            if (persist)
            {
                PlayerPrefs.SetString(LanguagePreferenceKey, languageCode);
                PlayerPrefs.Save();
            }
            onLanguageChangedSubject.OnNext(Unit.Default);
            return true;
        }
    }
}
