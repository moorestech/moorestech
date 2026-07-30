using System;
using System.Collections.Generic;
using System.Text;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

internal static class LanguageCatalogCodeEmitter
{
    public static void Emit(
        StringBuilder builder,
        string[] languageCodes,
        LanguageSetting[] settings)
    {
        ValidateLanguageSet(languageCodes, settings);

        // readonly値型と固定配列として言語表示メタを埋め込む
        // Embed language display metadata as a readonly value type and fixed array
        builder.AppendLine("    public readonly struct LanguageInfo");
        builder.AppendLine("    {");
        builder.AppendLine("        public readonly string Code;");
        builder.AppendLine("        public readonly string DisplayName;");
        builder.AppendLine("        public readonly string SteamApiLangCode;");
        builder.AppendLine();
        builder.AppendLine("        public LanguageInfo(string code, string displayName, string steamApiLangCode)");
        builder.AppendLine("        {");
        builder.AppendLine("            Code = code;");
        builder.AppendLine("            DisplayName = displayName;");
        builder.AppendLine("            SteamApiLangCode = steamApiLangCode;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static class LanguageCatalog");
        builder.AppendLine("    {");
        builder.AppendLine("        public static readonly LanguageInfo[] Languages = new LanguageInfo[]");
        builder.AppendLine("        {");
        foreach (var setting in settings)
        {
            builder.AppendLine(
                $"            new LanguageInfo(\"{LocalizationCodeSyntax.Escape(setting.Code)}\", \"{LocalizationCodeSyntax.Escape(setting.DisplayName)}\", \"{LocalizationCodeSyntax.Escape(setting.SteamApiLangCode)}\"),");
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void ValidateLanguageSet(
        string[] languageCodes,
        LanguageSetting[] settings)
    {
        var dictionaryLanguages = new HashSet<string>(languageCodes, StringComparer.Ordinal);
        if (dictionaryLanguages.Count != languageCodes.Length ||
            settings.Length != languageCodes.Length)
        {
            throw new LocalizationCsvException(
                "localization_settings.csv languages must exactly match localization.csv language columns");
        }

        // 設定側の重複と辞書側にない言語を同時に拒否
        // Reject both duplicate settings and languages absent from the dictionary
        var settingLanguages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            if (!settingLanguages.Add(setting.Code) ||
                !dictionaryLanguages.Contains(setting.Code))
            {
                throw new LocalizationCsvException(
                    "localization_settings.csv languages must exactly match localization.csv language columns");
            }
        }
    }
}
