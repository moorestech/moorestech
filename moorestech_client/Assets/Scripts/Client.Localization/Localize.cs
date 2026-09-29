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
        private static LanguageOrigin currentLanguageOrigin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            PublishSnapshot(VanillaLocalizationDictionaryFactory.Create());

            // 選択済み導出はここ1回だけ
            // Chosen state is derived only here
            var savedLanguageCode = PlayerPrefs.GetString(LanguagePreferenceKey, DefaultLanguageCode);
            var chosen = PlayerPrefs.HasKey(LanguagePreferenceKey) && IsSelectable(savedLanguageCode);
            currentLanguageCode = chosen ? savedLanguageCode : DefaultLanguageCode;
            currentLanguageOrigin = chosen ? LanguageOrigin.Chosen : LanguageOrigin.Unchosen;
            if (!chosen && PlayerPrefs.HasKey(LanguagePreferenceKey))
                Debug.LogWarning($"[Localize] saved language {savedLanguageCode} is unavailable; using {DefaultLanguageCode}");
        }

        public static string Get(LocalizationKey key)
        {
            var snapshot = Volatile.Read(ref publishedSnapshot);
            return LocalizationTextResolver.Resolve(snapshot, currentLanguageCode, key.Key);
        }

        public static string GetFormatted(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            return LocalizationTextInterpolator.Interpolate(Get(key), textParams);
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

        public static bool TrySetChosenLanguage(string languageCode)
        {
            return TryApplyLanguage(languageCode, LanguageOrigin.Chosen);
        }

        // 選択済みなら外部の言語ソースを読まない
        // Do not consult an external language source after the player has chosen
        public static bool TryApplyUnchosenLanguage(IUnchosenLanguageSource source)
        {
            if (currentLanguageOrigin == LanguageOrigin.Chosen)
            {
                Debug.Log("[Localize] temporary language rejected: the player already chose a language");
                return false;
            }
            if (!source.TryResolveGameLanguage(out var languageCode, out var failureReason))
            {
                Debug.LogWarning($"[Localize] temporary language unavailable: {failureReason}");
                return false;
            }
            return TryApplyLanguage(languageCode, LanguageOrigin.Unchosen);
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
            return Volatile.Read(ref publishedSnapshot)
                .TryGetDictionary(languageCode, expectedRevision, out dictionary);
        }

        public static bool TryGetSourceTexts(
            long expectedRevision,
            out IReadOnlyDictionary<string, string> sourceTexts)
        {
            return Volatile.Read(ref publishedSnapshot)
                .TryGetSourceTexts(expectedRevision, out sourceTexts);
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

        private static bool TryApplyLanguage(string languageCode, LanguageOrigin origin)
        {
            if (!IsSelectable(languageCode))
            {
                Debug.LogWarning($"[Localize] language rejected: unsupported code {languageCode ?? "<null>"}");
                return false;
            }

            // 明示選択のみ保存、共通イベントで通知
            // Persist only explicit choices; notify through the shared event
            currentLanguageCode = languageCode;
            currentLanguageOrigin = origin;
            if (origin == LanguageOrigin.Chosen) PersistCurrentLanguage();
            onLanguageChangedSubject.OnNext(Unit.Default);
            return true;
        }

        private static void PersistCurrentLanguage()
        {
            PlayerPrefs.SetString(LanguagePreferenceKey, currentLanguageCode);
            PlayerPrefs.Save();
        }
    }
}
