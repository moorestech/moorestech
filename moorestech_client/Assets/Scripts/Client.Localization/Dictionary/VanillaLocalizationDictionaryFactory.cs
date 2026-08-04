using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mooresmaster.Localization.Generated;

namespace Client.Localization
{
    internal static class VanillaLocalizationDictionaryFactory
    {
        public static LocalizationDictionaryCandidate Create()
        {
            var languages = new Dictionary<string, Dictionary<string, string>>();

            // 空訳を除き毎回新規構築
            // Build fresh dictionaries without empty translations
            foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
            {
                VanillaLocalizationTable.TryGetLanguage(languageCode, out var table);
                var languageDictionary = new Dictionary<string, string>();
                foreach (var entry in table)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    languageDictionary.Add(entry.Key, entry.Value);
                }

                languages.Add(languageCode, languageDictionary);
            }

            // 原文は実言語と別の辞書として同じcandidateへ載せる
            // Source texts ride on the same candidate as a dictionary distinct from the languages
            var sourceTexts = new Dictionary<string, string>();
            foreach (var entry in VanillaLocalizationTable.SourceTexts)
            {
                if (string.IsNullOrEmpty(entry.Value)) continue;
                sourceTexts.Add(entry.Key, entry.Value);
            }

            return new LocalizationDictionaryCandidate(languages, sourceTexts);
        }

        public static PublishedLocalizationDictionarySnapshot Freeze(
            LocalizationDictionaryCandidate candidate,
            long revision)
        {
            var frozenLanguages = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            // 非公開candidateだけをread-only viewで包みimmutable snapshot化する
            // Freeze the private candidate into immutable published read-only views
            foreach (var language in candidate.Languages)
            {
                frozenLanguages.Add(
                    language.Key,
                    new ReadOnlyDictionary<string, string>(language.Value));
            }

            return new PublishedLocalizationDictionarySnapshot(
                revision,
                new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(frozenLanguages),
                new ReadOnlyDictionary<string, string>(candidate.SourceTexts));
        }
    }
}
