using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Mooresmaster.LocalizationCsv;
using mooresmaster.Generator.Localization;

namespace mooresmaster.Generator;

public static class LocalizationSourceEmitter
{
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

    public static void Emit(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> additionalTexts)
    {
        var localizationFiles = FindLocalizationFiles(additionalTexts);
        if (localizationFiles.Count == 0)
        {
            return;
        }

        // 重複入力を順序固定の単一診断として報告する
        // Report duplicate inputs as one deterministically ordered diagnostic
        if (localizationFiles.Count > 1)
        {
            ReportError(context, CreateDuplicateFilesMessage(localizationFiles));
            return;
        }

        EmitLocalization(context, localizationFiles[0]);
    }

    private static List<AdditionalText> FindLocalizationFiles(
        ImmutableArray<AdditionalText> additionalTexts)
    {
        var files = new List<AdditionalText>();
        foreach (var additionalFile in additionalTexts)
        {
            if (Path.GetFileName(additionalFile.Path) == LocalizationFileName)
            {
                files.Add(additionalFile);
            }
        }

        return files;
    }

    private static void EmitLocalization(
        SourceProductionContext context,
        AdditionalText localizationFile)
    {
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

    private static string CreateDuplicateFilesMessage(List<AdditionalText> files)
    {
        var paths = new string[files.Count];
        for (var index = 0; index < files.Count; index++)
        {
            paths[index] = files[index].Path;
        }

        // 明示的なordinal挿入ソートで入力順を除去する
        // Remove input ordering with an explicit ordinal insertion sort
        for (var index = 1; index < paths.Length; index++)
        {
            var currentPath = paths[index];
            var insertionIndex = index - 1;
            while (insertionIndex >= 0 &&
                   string.CompareOrdinal(paths[insertionIndex], currentPath) > 0)
            {
                paths[insertionIndex + 1] = paths[insertionIndex];
                insertionIndex--;
            }

            paths[insertionIndex + 1] = currentPath;
        }

        var message = new StringBuilder("Multiple localization.csv files were found:");
        foreach (var path in paths)
        {
            message.Append(' ');
            message.Append(path);
        }

        return message.ToString();
    }

    private static void ReportError(SourceProductionContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, message));
    }
}
