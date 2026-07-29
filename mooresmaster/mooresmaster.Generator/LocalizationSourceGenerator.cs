using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;

namespace mooresmaster.Generator;

[Generator(LanguageNames.CSharp)]
public class LocalizationSourceGenerator : ISourceGenerator
{
    private const string EnabledDefine = "ENABLE_MOORESMASTER_GENERATOR";
    private const string LocalizationFileName = "localization.csv";
    private const string GeneratedSourceHintName = "mooresmaster.localization.g.cs";

    private static readonly DiagnosticDescriptor ErrorDescriptor = new(
        "MOORES003",
        "Mooresmaster Localization Error",
        "Localization source generator failed: {0}",
        "Mooresmaster",
        DiagnosticSeverity.Error,
        true
    );

    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.ParseOptions is not CSharpParseOptions csharpParseOptions ||
            !csharpParseOptions.PreprocessorSymbolNames.Contains(EnabledDefine))
        {
            return;
        }

        // AdditionalFileのbasenameを完全一致で探索する
        // Find the AdditionalFile by an exact basename match
        AdditionalText? localizationFile = null;
        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (Path.GetFileName(additionalFile.Path) != LocalizationFileName)
            {
                continue;
            }

            localizationFile = additionalFile;
            break;
        }

        if (localizationFile == null)
        {
            return;
        }

        var sourceText = localizationFile.GetText(context.CancellationToken);
        if (sourceText == null)
        {
            ReportError(context, $"Could not read '{localizationFile.Path}'");
            return;
        }

        // 外部CSV入力の不正だけをRoslyn診断へ変換する
        // Convert only invalid external CSV input into a Roslyn diagnostic
        try
        {
            var csv = LocalizationCsvParser.Parse(sourceText.ToString());
            var generatedCode = LocalizationCodeGenerator.Generate(csv);
            context.AddSource(GeneratedSourceHintName, SourceText.From(generatedCode, Encoding.UTF8));
        }
        catch (LocalizationCsvException exception)
        {
            ReportError(context, exception.Message);
        }
    }

    private static void ReportError(GeneratorExecutionContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, message));
    }
}
