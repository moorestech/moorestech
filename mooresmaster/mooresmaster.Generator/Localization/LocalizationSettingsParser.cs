using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

public record LanguageSetting(string Code, string DisplayName, string[] SteamLanguages)
{
    public readonly string Code = Code;
    public readonly string DisplayName = DisplayName;
    public readonly string[] SteamLanguages = SteamLanguages;
}

public static class LocalizationSettingsParser
{
    private const int ColumnCount = 3;

    public static LanguageSetting[] Parse(string csvText)
    {
        // 共通Parserでクォートを考慮したレコードへ分割
        // Split into quote-aware records with the shared parser
        var records = LocalizationCsvRecordReader.ParseRecords(csvText);
        if (records.Count == 0)
        {
            throw new LocalizationCsvException("localization_settings.csv is empty");
        }

        var header = records[0];
        if (header.Count != ColumnCount ||
            header[0] != "lang_name" ||
            header[1] != "display_name" ||
            header[2] != "steam_languages")
        {
            throw new LocalizationCsvException(
                "localization_settings.csv header must contain lang_name, display_name, and steam_languages columns");
        }

        // 言語コードを一意な設定行へ写像
        // Map language codes to unique setting rows
        var settings = new LanguageSetting[records.Count - 1];
        var seenCodes = new HashSet<string>();
        var seenSteamLanguages = new HashSet<string>();
        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var fields = records[recordIndex];
            if (fields.Count != ColumnCount)
            {
                throw new LocalizationCsvException(
                    $"Column count mismatch in localization_settings.csv at record {recordIndex + 1}: expected {ColumnCount}, got {fields.Count}");
            }

            var code = fields[0];
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new LocalizationCsvException("Language setting code must not be empty");
            }

            // 設定値を入力境界で検証
            // Require UI and Steam integration values at the input boundary
            var displayName = fields[1];
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new LocalizationCsvException("Language setting display name must not be empty");
            }

            // コード重複はSteam列より先に
            // Report duplicated codes first
            if (!seenCodes.Add(code))
            {
                throw new LocalizationCsvException($"Duplicated language setting code: {code}");
            }

            // 空要素と重複のSteam言語を拒否
            // Reject empty or duplicate Steam languages at input
            var steamLanguagesField = fields[2];
            if (string.IsNullOrWhiteSpace(steamLanguagesField))
            {
                throw new LocalizationCsvException("Language setting Steam languages must not be empty");
            }

            var steamLanguages = steamLanguagesField.Split(';');
            foreach (var steamLanguage in steamLanguages)
            {
                if (string.IsNullOrWhiteSpace(steamLanguage))
                {
                    throw new LocalizationCsvException($"Language setting {code} has an empty Steam language in: {steamLanguagesField}");
                }

                if (steamLanguage != steamLanguage.Trim())
                {
                    throw new LocalizationCsvException($"Language setting {code} has whitespace around Steam language '{steamLanguage}' in: {steamLanguagesField}");
                }

                if (!seenSteamLanguages.Add(steamLanguage))
                {
                    throw new LocalizationCsvException($"Steam language {steamLanguage} is mapped to more than one language");
                }
            }

            settings[recordIndex - 1] = new LanguageSetting(code, displayName, steamLanguages);
        }

        return settings;
    }
}
