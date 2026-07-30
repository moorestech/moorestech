using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mooresmaster.Localization.Generated;

namespace Client.Localization
{
    internal static class VanillaLocalizationDictionaryFactory
    {
        public static Dictionary<string, Dictionary<string, string>> Create()
        {
            var dictionaries = new Dictionary<string, Dictionary<string, string>>();

            // 空訳を除くバニラ言語辞書を毎回新規構築する
            // Build fresh vanilla language dictionaries without empty translations
            foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
            {
                VanillaLocalizationTable.TryGetLanguage(languageCode, out var table);
                var languageDictionary = new Dictionary<string, string>();
                foreach (var entry in table)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    languageDictionary.Add(entry.Key, entry.Value);
                }

                dictionaries.Add(languageCode, languageDictionary);
            }

            // Sourceも同じfresh candidateの擬似localeへ格納する
            // Store Source as a pseudo-locale in the same fresh candidate
            var sourceDictionary = new Dictionary<string, string>();
            foreach (var entry in VanillaLocalizationTable.SourceTexts)
            {
                if (string.IsNullOrEmpty(entry.Value)) continue;
                sourceDictionary.Add(entry.Key, entry.Value);
            }

            dictionaries.Add(Localize.SourcePseudoLocale, sourceDictionary);
            return dictionaries;
        }

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CreateSnapshot()
        {
            return Freeze(Create());
        }

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Freeze(
            Dictionary<string, Dictionary<string, string>> candidate)
        {
            var frozenLanguages = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            // 非公開candidateだけをread-only viewで包みimmutable snapshot化する
            // Freeze the private candidate into immutable published read-only views
            foreach (var language in candidate)
            {
                frozenLanguages.Add(
                    language.Key,
                    new ReadOnlyDictionary<string, string>(language.Value));
            }

            return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(frozenLanguages);
        }
    }
}
