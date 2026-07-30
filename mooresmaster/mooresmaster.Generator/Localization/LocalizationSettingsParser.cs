using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

public record LanguageSetting(string Code, string DisplayName, string SteamApiLangCode)
{
    public readonly string Code = Code;
    public readonly string DisplayName = DisplayName;
    public readonly string SteamApiLangCode = SteamApiLangCode;
}

public static class LocalizationSettingsParser
{
    private const int ColumnCount = 3;

    public static LanguageSetting[] Parse(string csvText)
    {
        // 共通Parserでクォートを考慮したレコードへ分割
        // Split into quote-aware records with the shared parser
        var records = LocalizationCsvParser.ParseRecords(csvText);
        if (records.Count == 0)
        {
            throw new LocalizationCsvException("localization_settings.csv is empty");
        }

        var header = records[0];
        if (header.Count != ColumnCount ||
            header[0] != "lang_name" ||
            header[1] != "display_name" ||
            header[2] != "steam_api_lang_code")
        {
            throw new LocalizationCsvException(
                "localization_settings.csv header must contain lang_name, display_name, and steam_api_lang_code columns");
        }

        // 言語コードを一意な設定行へ写像
        // Map language codes to unique setting rows
        var settings = new LanguageSetting[records.Count - 1];
        var seenCodes = new HashSet<string>();
        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var fields = records[recordIndex];
            if (fields.Count != ColumnCount)
            {
                throw new LocalizationCsvException(
                    $"Column count mismatch in localization_settings.csv at record {recordIndex + 1}: expected {ColumnCount}, got {fields.Count}");
            }

            var code = fields[0];
            if (string.IsNullOrEmpty(code))
            {
                throw new LocalizationCsvException("Language setting code must not be empty");
            }

            // UIとSteam連携に必要な設定値を入力境界で保証
            // Require UI and Steam integration values at the input boundary
            var displayName = fields[1];
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new LocalizationCsvException("Language setting display name must not be empty");
            }

            var steamApiLangCode = fields[2];
            if (string.IsNullOrWhiteSpace(steamApiLangCode))
            {
                throw new LocalizationCsvException("Language setting Steam API language code must not be empty");
            }

            if (!seenCodes.Add(code))
            {
                throw new LocalizationCsvException($"Duplicated language setting code: {code}");
            }

            settings[recordIndex - 1] = new LanguageSetting(code, displayName, steamApiLangCode);
        }

        return settings;
    }
}
