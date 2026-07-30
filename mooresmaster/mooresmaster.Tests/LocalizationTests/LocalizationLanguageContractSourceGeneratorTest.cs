using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using mooresmaster.Generator;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationLanguageContractSourceGeneratorTest
{
    [Fact]
    public void English言語列がなければMOORES003を報告する()
    {
        var result = RunGenerator(
            "key,Source,japanese\nui.menu.close,Close,閉じる\n");

        AssertContractDiagnostic(result);
    }

    [Fact]
    public void Source予約言語列があればMOORES003を報告する()
    {
        var result = RunGenerator(
            "key,Source,english,source\nui.menu.close,Close,Close,Close\n");

        AssertContractDiagnostic(result);
    }

    [Fact]
    public void 空言語コードがあればMOORES003を報告する()
    {
        var result = RunGenerator(
            "key,Source,english,\nui.menu.close,Close,Close,\n");

        AssertContractDiagnostic(result);
    }

    [Fact]
    public void 重複言語コードがあればMOORES003を報告する()
    {
        var result = RunGenerator(
            "key,Source,english,japanese,japanese\nui.menu.close,Close,Close,閉じる,閉じる\n");

        AssertContractDiagnostic(result);
    }

    private static GeneratorRunResult RunGenerator(string csvText)
    {
        var parseOptions = new CSharpParseOptions(
            preprocessorSymbols: new[] { "ENABLE_MOORESMASTER_GENERATOR" });
        var compilation = CSharpCompilation.Create("LocalizationLanguageContractSourceGeneratorTest");
        var additionalTexts = new AdditionalText[]
        {
            new TestAdditionalText("/content/localization.csv", csvText),
            new TestAdditionalText(
                "/content/localization_settings.csv",
                "lang_name,display_name,steam_api_lang_code\nenglish,English,en\njapanese,日本語,ja\n"),
        };

        // 実RoslynでCSV契約診断を検証
        // Verify CSV contract diagnostics with Roslyn
        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new MooresmasterSourceGenerator().AsSourceGenerator() },
            additionalTexts,
            parseOptions);
        return driver.RunGenerators(compilation).GetRunResult().Results[0];
    }

    private static void AssertContractDiagnostic(GeneratorRunResult result)
    {
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("MOORES003", diagnostic.Id);
        Assert.Equal(0, CountLocalizationSources(result));
    }

    private static int CountLocalizationSources(GeneratorRunResult result)
    {
        var count = 0;
        foreach (var source in result.GeneratedSources)
        {
            if (source.HintName == "mooresmaster.localization.g.cs") count++;
        }

        return count;
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly string path;
        private readonly SourceText sourceText;

        public TestAdditionalText(string path, string text)
        {
            this.path = path;
            sourceText = SourceText.From(text, Encoding.UTF8);
        }

        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken)
        {
            return sourceText;
        }
    }
}
