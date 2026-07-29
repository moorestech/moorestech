using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using mooresmaster.Generator;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationSourceGeneratorTest
{
    [Fact]
    public void DefineがなければCSVがあっても生成しない()
    {
        var csvFile = new TestAdditionalText(
            "/content/localization.csv",
            "key,Source,english\nui.menu.close,Close,Close\n");

        var result = RunGenerator(false, new[] { csvFile });

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LocalizationCsvがなければ生成しない()
    {
        var otherFile = new TestAdditionalText(
            "/content/translations.csv",
            "key,Source,english\nui.menu.close,Close,Close\n");

        var result = RunGenerator(true, new[] { otherFile });

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void 有効なLocalizationCsvを固定hint名で生成する()
    {
        var csvFile = new TestAdditionalText(
            "/content/localization.csv",
            "key,Source,english\nui.menu.close,Close,Close\n");

        var result = RunGenerator(true, new[] { csvFile });
        var generatedSource = Assert.Single(result.GeneratedSources);

        Assert.Equal("mooresmaster.localization.g.cs", generatedSource.HintName);
        Assert.Contains("public static readonly LocalizationKey Close", generatedSource.SourceText.ToString());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void 不正なLocalizationCsvはMOORES003を報告して生成しない()
    {
        var csvFile = new TestAdditionalText(
            "/content/localization.csv",
            "key,Source,english\nui.menu.close,Close\n");

        var result = RunGenerator(true, new[] { csvFile });
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("MOORES003", diagnostic.Id);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void LocalizationCsvのbasenameは大文字小文字を区別する()
    {
        var csvFile = new TestAdditionalText(
            "/content/Localization.csv",
            "key,Source,english\nui.menu.close,Close,Close\n");

        var result = RunGenerator(true, new[] { csvFile });

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LocalizationCsvを読めなければMOORES003を報告して生成しない()
    {
        var csvFile = new TestAdditionalText("/content/localization.csv", null);

        var result = RunGenerator(true, new[] { csvFile });
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("MOORES003", diagnostic.Id);
        Assert.Empty(result.GeneratedSources);
    }

    private static GeneratorRunResult RunGenerator(
        bool enabled,
        IEnumerable<AdditionalText> additionalTexts)
    {
        var symbols = enabled
            ? new[] { "ENABLE_MOORESMASTER_GENERATOR" }
            : System.Array.Empty<string>();
        var parseOptions = new CSharpParseOptions(preprocessorSymbols: symbols);
        var compilation = CSharpCompilation.Create("LocalizationSourceGeneratorTest");

        // 実Roslyn Driverでdefine・AdditionalFile・生成結果を一体検証する
        // Exercise defines, additional files, and output together through the real Roslyn driver
        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new LocalizationSourceGenerator() },
            additionalTexts,
            parseOptions);
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly string path;
        private readonly SourceText? sourceText;

        public TestAdditionalText(string path, string? text)
        {
            this.path = path;
            sourceText = text == null ? null : SourceText.From(text, Encoding.UTF8);
        }

        // Roslynの抽象AdditionalText契約をテスト入力として実装する
        // Implement Roslyn's abstract AdditionalText contract as a test input
        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken)
        {
            return sourceText;
        }
    }
}
