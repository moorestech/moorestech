using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using mooresmaster.Generator;
using Xunit;

namespace mooresmaster.Tests.LocalizationTests;

public class LocalizationSourcePairContractTest
{
    [Theory]
    [InlineData("/content/localization.csv", "key,Source,english\nui.menu.close,Close,Close\n")]
    [InlineData("/content/localization_settings.csv", "lang_name,display_name,steam_api_lang_code\nenglish,English,en\n")]
    [InlineData("/content/content_keys.csv", "namespace,field,sourceMaster\nitem,name,ItemMaster\n")]
    public void 辞書と設定と宣言表の一部だけならMOORES003を報告する(string path, string text)
    {
        var result = RunGenerator(new TestAdditionalText(path, text));

        Assert.Equal("MOORES003", Assert.Single(result.Diagnostics).Id);
        Assert.Equal(0, CountLocalizationSources(result));
    }

    private static GeneratorRunResult RunGenerator(AdditionalText additionalText)
    {
        var parseOptions = new CSharpParseOptions(
            preprocessorSymbols: new[] { "ENABLE_MOORESMASTER_GENERATOR" });
        var compilation = CSharpCompilation.Create("LocalizationSourcePairContractTest");

        // 実RoslynでAdditionalFilesの対契約を検証
        // Verify the AdditionalFiles pair contract with real Roslyn
        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new MooresmasterSourceGenerator().AsSourceGenerator() },
            new[] { additionalText },
            parseOptions);
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
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
