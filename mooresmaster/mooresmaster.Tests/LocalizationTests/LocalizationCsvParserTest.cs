using Mooresmaster.LocalizationCsv;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationCsvParserTest
{
    [Fact]
    public void ヘッダから言語コードを取得できる()
    {
        var csv = "key,Source,english,japanese\nui.a.b,Hello,Hello,こんにちは\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal(new[] { "english", "japanese" }, result.LanguageCodes);
    }

    [Fact]
    public void 行のキーとテキストを取得できる()
    {
        var csv = "key,Source,english,japanese\nui.a.b,Hello,Hello,こんにちは\n";

        var result = LocalizationCsvParser.Parse(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal("ui.a.b", row.Key);
        Assert.Equal("Hello", row.Source);
        Assert.Equal(new[] { "Hello", "こんにちは" }, row.Texts);
    }

    [Fact]
    public void ダブルクォート内のカンマを扱える()
    {
        var csv = "key,Source,english,japanese\nui.a.b,\"Hi, you\",\"Hi, you\",こんにちは\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("Hi, you", result.Rows[0].Source);
        Assert.Equal("Hi, you", result.Rows[0].Texts[0]);
    }

    [Fact]
    public void ダブルクォート内の実改行を扱える()
    {
        var csv = "key,Source,english,japanese\nui.a.b,\"First\nSecond\",\"One\nTwo\",日本語\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("First\nSecond", result.Rows[0].Source);
        Assert.Equal("One\nTwo", result.Rows[0].Texts[0]);
    }

    [Fact]
    public void エスケープされたダブルクォートを扱える()
    {
        var csv = "key,Source,english,japanese\nui.a.b,\"Say \"\"Hi\"\"\",\"Say \"\"Hi\"\"\",日本語\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("Say \"Hi\"", result.Rows[0].Source);
        Assert.Equal("Say \"Hi\"", result.Rows[0].Texts[0]);
    }

    [Fact]
    public void CrLfのレコード区切りを扱える()
    {
        var csv = "key,Source,english,japanese\r\nui.a.b,Hello,Hello,こんにちは\r\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("japanese", result.LanguageCodes[1]);
        Assert.Equal("こんにちは", result.Rows[0].Texts[1]);
    }

    [Fact]
    public void 終端の空翻訳fieldを保持する()
    {
        var csv = "key,Source,english,japanese\nui.a,Source,English,";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("", result.Rows[0].Texts[1]);
    }

    [Fact]
    public void Source列と全翻訳列の改行エスケープを実改行へ変換する()
    {
        var csv = "key,Source,english,japanese\nui.a,Author\\nNote,English\\nText,日本語\\n訳文\n";

        var result = LocalizationCsvParser.Parse(csv);

        Assert.Equal("Author\nNote", result.Rows[0].Source);
        Assert.Equal(new[] { "English\nText", "日本語\n訳文" }, result.Rows[0].Texts);
    }

    [Fact]
    public void キー重複は例外()
    {
        var csv = "key,Source,english,japanese\nui.a,x,x,x\nui.a,y,y,y\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void 列数不足は例外()
    {
        var csv = "key,Source,english,japanese\nui.a,x,x\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void 列数超過は例外()
    {
        var csv = "key,Source,english,japanese\nui.a,x,x,x,extra\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void 空CSVは例外()
    {
        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(""));
    }

    [Theory]
    [InlineData("Source,key,english")]
    [InlineData("id,Source,english")]
    [InlineData("key,source,english")]
    public void key列とSource列の名前または順序が不正なら例外(string header)
    {
        var csv = $"{header}\nui.a,Source,English\n";

        var exception = Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));

        Assert.Contains("key and Source columns", exception.Message);
    }

    [Fact]
    public void 閉じていないダブルクォートは例外()
    {
        var csv = "key,Source,english\nui.a,\"Source,English\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void 閉じたダブルクォート後の文字は例外()
    {
        var csv = "key,Source,english\nui.a,\"Source\"x,English\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }

    [Fact]
    public void ParseRecordsはクォートと空fieldを保持する()
    {
        var records = LocalizationCsvParser.ParseRecords("a,\"b,b\",\n");

        var record = Assert.Single(records);
        Assert.Equal(new[] { "a", "b,b", "" }, record);
    }

    [Fact]
    public void ParseRecordsは空の物理行を無視する()
    {
        var records = LocalizationCsvParser.ParseRecords("\n");

        Assert.Empty(records);
    }

    [Fact]
    public void ParseRecordsは明示的なquotedEmptyRecordを保持する()
    {
        var records = LocalizationCsvParser.ParseRecords("\"\"\n");

        var record = Assert.Single(records);
        Assert.Equal(new[] { "" }, record);
    }

    [Fact]
    public void 明示的なquotedEmptyRecordの列数不一致は例外()
    {
        var csv = "key,Source,english\n\"\"\n";

        Assert.Throws<LocalizationCsvException>(() => LocalizationCsvParser.Parse(csv));
    }
}
