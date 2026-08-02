using System.Text;

namespace mooresmaster.Generator.Localization;

internal static class ContentKeyCodeEmitter
{
    public static void Emit(StringBuilder builder, ContentKeyDefinition[] definitions)
    {
        // 生文字列と混同できない値型として導出キーを表す
        // Represent derived keys as a value type that cannot be confused with a raw string
        builder.AppendLine("    public readonly struct ContentLocalizationKey");
        builder.AppendLine("    {");
        builder.AppendLine("        public readonly string Key;");
        builder.AppendLine("        public ContentLocalizationKey(string key) { Key = key; }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static class ContentLocalizationKeys");
        builder.AppendLine("    {");
        foreach (var definition in definitions)
        {
            EmitBuilder(definition);
        }

        builder.AppendLine("    }");
        builder.AppendLine();

        #region Internal

        void EmitBuilder(ContentKeyDefinition definition)
        {
            // 宣言表の1行を<namespace>.<小文字D形式guid>.<field>のビルダーへ写す
            // Map one declaration row to a <namespace>.<lowercase D guid>.<field> builder
            var builderName = LocalizationCodeSyntax.ToPascalCase(definition.Namespace) +
                              LocalizationCodeSyntax.ToPascalCase(definition.Field);
            var parameterName = $"{definition.Namespace}Guid";
            builder.AppendLine($"        // source text: {definition.SourceMaster}");
            builder.AppendLine($"        public static ContentLocalizationKey {builderName}(System.Guid {parameterName})");
            builder.AppendLine("        {");
            builder.AppendLine(
                $"            return new ContentLocalizationKey(\"{definition.Namespace}.\" + {parameterName}.ToString(\"D\") + \".{definition.Field}\");");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        #endregion
    }
}
