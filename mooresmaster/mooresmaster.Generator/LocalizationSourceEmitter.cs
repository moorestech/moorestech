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
    private const string LocalizationSettingsFileName = "localization_settings.csv";
    private const string ContentKeyCatalogFileName = "content_keys.csv";
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
        var localizationFiles = FindFiles(additionalTexts, LocalizationFileName);
        var settingsFiles = FindFiles(additionalTexts, LocalizationSettingsFileName);
        var contentKeyFiles = FindFiles(additionalTexts, ContentKeyCatalogFileName);
        if (localizationFiles.Count == 0 && settingsFiles.Count == 0 && contentKeyFiles.Count == 0)
        {
            return;
        }

        // 重複入力を順序固定の単一診断として報告する
        // Report duplicate inputs as one deterministically ordered diagnostic
        if (1 < localizationFiles.Count)
        {
            ReportError(
                context,
                CreateDuplicateFilesMessage(LocalizationFileName, localizationFiles));
            return;
        }

        if (1 < settingsFiles.Count)
        {
            ReportError(
                context,
                CreateDuplicateFilesMessage(LocalizationSettingsFileName, settingsFiles));
            return;
        }

        if (1 < contentKeyFiles.Count)
        {
            ReportError(
                context,
                CreateDuplicateFilesMessage(ContentKeyCatalogFileName, contentKeyFiles));
            return;
        }

        // 一部だけのAdditionalFiles配線をコンパイルエラーにする
        // Turn partial AdditionalFiles wiring into a compilation error
        if (localizationFiles.Count == 0 || settingsFiles.Count == 0 || contentKeyFiles.Count == 0)
        {
            ReportError(
                context,
                "localization.csv, localization_settings.csv, and content_keys.csv must all be provided");
            return;
        }

        EmitLocalization();
        return;

        #region Internal

        void EmitLocalization()
        {
            var sourceText = localizationFiles[0].GetText(context.CancellationToken);
            if (sourceText == null)
            {
                ReportError(context, $"Could not read '{localizationFiles[0].Path}'");
                return;
            }

            var settingsText = settingsFiles[0].GetText(context.CancellationToken);
            if (settingsText == null)
            {
                ReportError(context, $"Could not read '{settingsFiles[0].Path}'");
                return;
            }

            var contentKeyText = contentKeyFiles[0].GetText(context.CancellationToken);
            if (contentKeyText == null)
            {
                ReportError(context, $"Could not read '{contentKeyFiles[0].Path}'");
                return;
            }

            // 外部CSV入力の不正だけをRoslyn診断へ変換する
            // Convert only invalid external CSV input into a Roslyn diagnostic
            try
            {
                var csv = LocalizationCsvParser.Parse(sourceText.ToString());
                var settings = LocalizationSettingsParser.Parse(settingsText.ToString());
                var contentKeys = ContentKeyCatalogParser.Parse(contentKeyText.ToString());
                LocalizationLanguageContract.Validate(csv);
                var generatedCode = LocalizationCodeGenerator.Generate(csv, settings, contentKeys);
                context.AddSource(GeneratedSourceHintName, SourceText.From(generatedCode, Encoding.UTF8));
            }
            catch (LocalizationCsvException exception)
            {
                ReportError(context, exception.Message);
            }
        }

        #endregion
    }

    private static List<AdditionalText> FindFiles(
        ImmutableArray<AdditionalText> additionalTexts,
        string fileName)
    {
        var files = new List<AdditionalText>();
        foreach (var additionalFile in additionalTexts)
        {
            if (Path.GetFileName(additionalFile.Path) == fileName)
            {
                files.Add(additionalFile);
            }
        }

        return files;
    }

    private static string CreateDuplicateFilesMessage(
        string fileName,
        List<AdditionalText> files)
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
            while (0 <= insertionIndex &&
                   0 < string.CompareOrdinal(paths[insertionIndex], currentPath))
            {
                paths[insertionIndex + 1] = paths[insertionIndex];
                insertionIndex--;
            }

            paths[insertionIndex + 1] = currentPath;
        }

        var message = new StringBuilder($"Multiple {fileName} files were found:");
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
