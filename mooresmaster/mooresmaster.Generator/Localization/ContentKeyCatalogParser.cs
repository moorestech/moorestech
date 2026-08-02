using System.Collections.Generic;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

public record ContentKeyDefinition(string Namespace, string Field, string SourceMaster)
{
    public readonly string Namespace = Namespace;
    public readonly string Field = Field;
    public readonly string SourceMaster = SourceMaster;
}

public static class ContentKeyCatalogParser
{
    private const int ColumnCount = 3;

    public static ContentKeyDefinition[] Parse(string csvText)
    {
        // 共通Parserでクォートを考慮したレコードへ分割
        // Split into quote-aware records with the shared parser
        var records = LocalizationCsvParser.ParseRecords(csvText);
        if (records.Count == 0)
        {
            throw new LocalizationCsvException("content_keys.csv is empty");
        }

        var header = records[0];
        if (header.Count != ColumnCount ||
            header[0] != "namespace" ||
            header[1] != "field" ||
            header[2] != "sourceMaster")
        {
            throw new LocalizationCsvException(
                "content_keys.csv header must contain namespace, field, and sourceMaster columns");
        }

        // 導出キーの名前空間とfieldを一意な宣言行へ写像
        // Map derived-key namespaces and fields to unique declaration rows
        var definitions = new ContentKeyDefinition[records.Count - 1];
        var seenKeys = new HashSet<string>();
        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var fields = records[recordIndex];
            if (fields.Count != ColumnCount)
            {
                throw new LocalizationCsvException(
                    $"Column count mismatch in content_keys.csv at record {recordIndex + 1}: expected {ColumnCount}, got {fields.Count}");
            }

            // キーsegmentは辞書側と同じlowerCamel契約を課す
            // Require the same lower-camel segment contract as the dictionary side
            var contentNamespace = fields[0];
            var field = fields[1];
            if (!LocalizationCodeSyntax.IsLowerCamelSegment(contentNamespace) ||
                !LocalizationCodeSyntax.IsLowerCamelSegment(field))
            {
                throw new LocalizationCsvException(
                    $"Content key segments '{contentNamespace}' and '{field}' must match [a-z][A-Za-z0-9]*");
            }

            var sourceMaster = fields[2];
            if (string.IsNullOrWhiteSpace(sourceMaster))
            {
                throw new LocalizationCsvException(
                    $"Content key '{contentNamespace}.{field}' must declare the Master that supplies its source text");
            }

            if (!seenKeys.Add($"{contentNamespace}.{field}"))
            {
                throw new LocalizationCsvException($"Duplicated content key: {contentNamespace}.{field}");
            }

            definitions[recordIndex - 1] = new ContentKeyDefinition(contentNamespace, field, sourceMaster);
        }

        return definitions;
    }
}
