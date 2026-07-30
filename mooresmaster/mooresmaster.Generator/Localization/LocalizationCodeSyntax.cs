using System.Text;
using Mooresmaster.LocalizationCsv;

namespace mooresmaster.Generator.Localization;

internal static class LocalizationCodeSyntax
{
    public static void ValidateKeys(LocalizationCsv csv)
    {
        // 生成可能なキーsegmentだけ許可
        // Allow only generatable key segments
        foreach (var row in csv.Rows)
        {
            var segments = row.Key.Split('.');
            var containingTypeName = "LocalizationKeys";
            foreach (var segment in segments)
            {
                if (!IsLowerCamelSegment(segment))
                {
                    throw new LocalizationCsvException(
                        $"Localization key segment '{segment}' in key '{row.Key}' must match [a-z][A-Za-z0-9]*");
                }

                // C#で禁止される親型と同名のメンバー生成を防ぐ
                // Prevent members from receiving their containing type's forbidden name
                var generatedName = ToPascalCase(segment);
                if (generatedName == containingTypeName)
                {
                    throw new LocalizationCsvException(
                        $"Localization key segment '{segment}' in key '{row.Key}' conflicts with its containing type");
                }

                containingTypeName = generatedName;
            }
        }
    }

    public static string Escape(string text)
    {
        var builder = new StringBuilder();
        // 許可制御文字だけC#用にescape
        // Escape only approved control characters for C#
        foreach (var character in text)
        {
            if (character == '\\') builder.Append("\\\\");
            else if (character == '"') builder.Append("\\\"");
            else if (character == '\n') builder.Append("\\n");
            else if (character == '\r') builder.Append("\\r");
            else if (character == '\u2028') builder.Append("\\u2028");
            else if (character == '\u2029') builder.Append("\\u2029");
            else if (char.IsControl(character))
                throw new LocalizationCsvException($"Unsupported control character U+{(int)character:X4} in localization text");
            else builder.Append(character);
        }

        return builder.ToString();
    }

    public static string ToPascalCase(string segment)
    {
        return char.ToUpperInvariant(segment[0]) + segment.Substring(1);
    }

    private static bool IsLowerCamelSegment(string segment)
    {
        // lowerCamel契約を境界検査
        // Validate the lower-camel contract at the boundary
        if (segment.Length == 0 || segment[0] < 'a' || 'z' < segment[0])
        {
            return false;
        }

        for (var index = 1; index < segment.Length; index++)
        {
            var character = segment[index];
            var isLetter = 'a' <= character && character <= 'z' || 'A' <= character && character <= 'Z';
            if (!isLetter && (character < '0' || '9' < character))
            {
                return false;
            }
        }

        return true;
    }
}
