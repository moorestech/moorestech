using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LanguageCatalogTest
{
    private const string DictionaryCsv = "key,Source,english,japanese\nui.a.b,x,x,y\n";
    private const string SettingsCsv = "lang_name,display_name,steam_api_lang_code\nenglish,English,en\njapanese,日本語,ja\n";

    [Fact]
    public void LanguageCatalogが生成される()
    {
        var code = LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(SettingsCsv));

        Assert.Contains("LanguageCatalog", code);
        Assert.Contains("日本語", code);
        Assert.Contains("\"ja\"", code);
    }

    [Fact]
    public void 言語セット不一致は例外()
    {
        const string settingsMissingJapanese =
            "lang_name,display_name,steam_api_lang_code\nenglish,English,en\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(settingsMissingJapanese)));
    }

    [Fact]
    public void 設定値のquotedCommaを保持する()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_api_lang_code\nenglish,\"English, Global\",en\n";

        var setting = Assert.Single(LocalizationSettingsParser.Parse(settingsCsv));

        Assert.Equal("english", setting.Code);
        Assert.Equal("English, Global", setting.DisplayName);
        Assert.Equal("en", setting.SteamApiLangCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void 言語コードが空または空白なら例外(string languageCode)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_api_lang_code\n{languageCode},English,en\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void 表示名が空または空白なら例外(string displayName)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_api_lang_code\nenglish,{displayName},en\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Steam言語コードが空または空白なら例外(string steamApiLangCode)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_api_lang_code\nenglish,English,{steamApiLangCode}\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }
}
