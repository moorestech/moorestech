using System;
using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;
using Xunit;
using static mooresmaster.Tests.LocalizationTests.LocalizationGeneratedCodeCompiler;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationCodeGeneratorTest
{
    [Fact]
    public void ネストキーと有効なCSharpソースを生成する()
    {
        const string csvText = "key,Source,english,japanese\nui.buildMenu.close,Close,Close,閉じる\n";

        var code = LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(csvText));
        var tableType = CompileTable(code);

        // 生成物の公開キー構造と値を実アセンブリから検証する
        // Verify the generated public key structure and value from a real assembly
        Assert.Contains("public static class Ui", code);
        Assert.Contains("public static class BuildMenu", code);
        Assert.Contains("public static readonly LocalizationKey Close = new LocalizationKey(\"ui.buildMenu.close\");", code);
        var buildMenuType = tableType.Assembly.GetType(
            "Mooresmaster.Localization.Generated.LocalizationKeys+Ui+BuildMenu")!;
        var localizationKey = buildMenuType.GetField("Close")!.GetValue(null)!;
        Assert.Equal("ui.buildMenu.close", localizationKey.GetType().GetField("Key")!.GetValue(localizationKey));
    }

    [Fact]
    public void 全行の言語辞書とSource列を空文字を含めて保持する()
    {
        const string csvText =
            "key,Source,english,japanese\n" +
            "ui.menu.close,Close,Close,閉じる\n" +
            "ui.menu.empty,,Empty,\n";

        var tableType = CompileTable(LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(csvText)));
        var languageCodes = (string[])tableType.GetField("LanguageCodes")!.GetValue(null)!;
        var sourceTexts = (IReadOnlyDictionary<string, string>)tableType.GetField("SourceTexts")!.GetValue(null)!;

        // 公開APIから言語順・全行・空文字の保持を観測する
        // Observe language order, every row, and empty text through the public API
        Assert.Equal(new[] { "english", "japanese" }, languageCodes);
        Assert.Equal("Close", sourceTexts["ui.menu.close"]);
        Assert.Equal("", sourceTexts["ui.menu.empty"]);
        AssertLanguage(tableType, "english", "ui.menu.empty", "Empty");
        AssertLanguage(tableType, "japanese", "ui.menu.empty", "");
    }

    [Fact]
    public void TryGetLanguageは未知言語でfalseとnullを返す()
    {
        const string csvText = "key,Source,english\nui.menu.close,Close,Close\n";
        var tableType = CompileTable(LocalizationCodeGenerator.Generate(LocalizationCsvParser.Parse(csvText)));
        var arguments = new object?[] { "unknown", null };

        var found = (bool)tableType.GetMethod("TryGetLanguage")!.Invoke(null, arguments)!;

        Assert.False(found);
        Assert.Null(arguments[1]);
    }

    [Fact]
    public void 言語列がなくても空のLanguageCodesを生成できる()
    {
        var csv = new LocalizationCsv(
            Array.Empty<string>(),
            new[] { new LocalizationRow("ui.menu.close", "Close", Array.Empty<string>()) });

        var tableType = CompileTable(LocalizationCodeGenerator.Generate(csv));
        var languageCodes = (string[])tableType.GetField("LanguageCodes")!.GetValue(null)!;

        Assert.Empty(languageCodes);
    }

    [Fact]
    public void quoteとbackslashと改行コードを値として復元できる()
    {
        var escapedText = "He said \"hi\" at C:\\work\nnext\rreturn";
        var csv = new LocalizationCsv(
            new[] { "english" },
            new[] { new LocalizationRow("ui.message.body", escapedText, new[] { escapedText }) });

        var tableType = CompileTable(LocalizationCodeGenerator.Generate(csv));

        // C#リテラルを壊さず全エスケープ対象を往復させる
        // Round-trip every escaped character without breaking the C# literal
        AssertLanguage(tableType, "english", "ui.message.body", escapedText);
        var sourceTexts = (IReadOnlyDictionary<string, string>)tableType.GetField("SourceTexts")!.GetValue(null)!;
        Assert.Equal(escapedText, sourceTexts["ui.message.body"]);
    }

    [Theory]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void Unicode改行文字をCSharpリテラルから値として復元できる(string separator)
    {
        var languageCode = $"english{separator}code";
        var text = $"before{separator}after";
        var csv = new LocalizationCsv(
            new[] { languageCode },
            new[] { new LocalizationRow("ui.message.body", text, new[] { text }) });

        var tableType = CompileTable(LocalizationCodeGenerator.Generate(csv));
        var languageCodes = (string[])tableType.GetField("LanguageCodes")!.GetValue(null)!;
        var sourceTexts = (IReadOnlyDictionary<string, string>)tableType.GetField("SourceTexts")!.GetValue(null)!;

        Assert.Equal(languageCode, Assert.Single(languageCodes));
        Assert.Equal(text, sourceTexts["ui.message.body"]);
        AssertLanguage(tableType, languageCode, "ui.message.body", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("build-menu")]
    [InlineData("build_menu")]
    [InlineData("BuildMenu")]
    [InlineData("1buildMenu")]
    public void lowerCamelでないキーsegmentは明示例外(string invalidSegment)
    {
        var csv = new LocalizationCsv(
            new[] { "english" },
            new[] { new LocalizationRow($"ui.{invalidSegment}.close", "", new[] { "" }) });

        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(csv));
    }

    [Theory]
    [InlineData("localizationKeys.close")]
    [InlineData("ui.ui")]
    public void 親型と同名になるキーsegmentは明示例外(string key)
    {
        var csv = new LocalizationCsv(new[] { "english" }, new[] { new LocalizationRow(key, "", new[] { "" }) });

        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(csv));
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\0")]
    [InlineData("\u001F")]
    public void CSharpリテラルで未対応の制御文字は明示例外(string controlCharacter)
    {
        var csv = new LocalizationCsv(
            new[] { "english" },
            new[] { new LocalizationRow("ui.message.body", controlCharacter, new[] { controlCharacter }) });

        Assert.Throws<LocalizationCsvException>(() => LocalizationCodeGenerator.Generate(csv));
    }

    private static void AssertLanguage(
        Type tableType,
        string languageCode,
        string key,
        string expectedText)
    {
        var arguments = new object?[] { languageCode, null };
        var found = (bool)tableType.GetMethod("TryGetLanguage")!.Invoke(null, arguments)!;
        var dictionary = (IReadOnlyDictionary<string, string>)arguments[1]!;

        Assert.True(found);
        Assert.Equal(expectedText, dictionary[key]);
    }

}
