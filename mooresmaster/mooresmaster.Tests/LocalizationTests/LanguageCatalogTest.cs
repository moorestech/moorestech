using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LanguageCatalogTest
{
    private const string DictionaryCsv = "key,Source,english,japanese\nui.a.b,x,x,y\n";
    private const string SettingsCsv = "lang_name,display_name,steam_languages\nenglish,English,english\njapanese,日本語,japanese\n";

    [Fact]
    public void LanguageCatalogが生成される()
    {
        var code = LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(SettingsCsv),
            System.Array.Empty<ContentKeyDefinition>());

        Assert.Contains("LanguageCatalog", code);
        Assert.Contains("日本語", code);
        Assert.Contains("new string[] { \"japanese\" }", code);
    }

    [Fact]
    public void 言語セット不一致は例外()
    {
        const string settingsMissingJapanese =
            "lang_name,display_name,steam_languages\nenglish,English,english\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(
            LocalizationCsvParser.Parse(DictionaryCsv),
            LocalizationSettingsParser.Parse(settingsMissingJapanese),
            System.Array.Empty<ContentKeyDefinition>()));
    }

    [Fact]
    public void 設定値のquotedCommaを保持する()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_languages\nenglish,\"English, Global\",english\n";

        var setting = Assert.Single(LocalizationSettingsParser.Parse(settingsCsv));

        Assert.Equal("english", setting.Code);
        Assert.Equal("English, Global", setting.DisplayName);
        Assert.Equal(new[] { "english" }, setting.SteamLanguages);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void 言語コードが空または空白なら例外(string languageCode)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\n{languageCode},English,english\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void 表示名が空または空白なら例外(string displayName)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\nenglish,{displayName},english\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Steam言語コードが空または空白なら例外(string steamLanguages)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\nenglish,English,{steamLanguages}\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Fact]
    public void Steam言語はセミコロン区切りで複数持てる()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_languages\nspanish,Español,spanish;latam\n";

        var setting = Assert.Single(LocalizationSettingsParser.Parse(settingsCsv));

        Assert.Equal(new[] { "spanish", "latam" }, setting.SteamLanguages);
    }

    [Theory]
    [InlineData("spanish;")]
    [InlineData(";latam")]
    [InlineData("spanish; ;latam")]
    public void Steam言語に空要素があれば例外(string steamLanguages)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\nspanish,Español,{steamLanguages}\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Theory]
    [InlineData("spanish; latam")]
    [InlineData(" spanish;latam")]
    public void Steam言語に前後空白があれば例外(string steamLanguages)
    {
        var settingsCsv =
            $"lang_name,display_name,steam_languages\nspanish,Español,{steamLanguages}\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }

    [Fact]
    public void 同じSteam言語を二つの言語へ対応づけたら例外()
    {
        const string settingsCsv =
            "lang_name,display_name,steam_languages\nspanish,Español,spanish;latam\nlatam_spanish,Español (LA),latam\n";

        Assert.Throws<LocalizationCsvException>(() =>
            LocalizationSettingsParser.Parse(settingsCsv));
    }
}
