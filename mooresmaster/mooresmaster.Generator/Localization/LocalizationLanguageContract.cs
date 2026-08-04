using System;
using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

internal static class LocalizationLanguageContract
{
    private const string RequiredEnglishLanguageCode = "english";
    private const string ReservedSourceLanguageCode = "source";

    public static void Validate(LocalizationCsv csv)
    {
        var englishCount = 0;
        var languageCodes = new HashSet<string>(StringComparer.Ordinal);

        // 言語コードの重複を拒否
        // Reject duplicate language codes
        foreach (var languageCode in csv.LanguageCodes)
        {
            if (string.IsNullOrEmpty(languageCode))
                throw new LocalizationCsvException("Language code must not be empty");
            if (languageCode == ReservedSourceLanguageCode)
                throw new LocalizationCsvException("Language code 'source' is reserved");
            if (!languageCodes.Add(languageCode))
                throw new LocalizationCsvException($"Duplicated language code: {languageCode}");
            if (languageCode == RequiredEnglishLanguageCode) englishCount++;
        }

        // 実行時fallbackの正本となる英語列を必須化する
        // Require the English column used as the runtime fallback source
        if (englishCount != 1)
            throw new LocalizationCsvException("Language code 'english' must appear exactly once");
    }
}
