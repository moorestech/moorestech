using System.Globalization;

namespace Client.Skit.Localization
{
    public static class SkitCommandLocalization
    {
        // skitキー名前空間の唯一の定義。loaderのフィルタと必ず同一値を共有する
        // Single definition of the skit key namespace, shared with the loader filter
        public const string KeyPrefix = "skit.";

        public const string BodyField = "body";
        public const string Option1Field = "Option1Tag";
        public const string Option2Field = "Option2Tag";
        public const string Option3Field = "Option3Tag";
        public const string OverrideCharacterNameField = "overrideCharacterName";

        public static string CreateKey(string skitTitle, int commandId, string field)
        {
            return $"{KeyPrefix}{skitTitle}.{commandId.ToString(CultureInfo.InvariantCulture)}.{field}";
        }

        public static ResolvedSkitLine ResolveLine(
            ISkitLocalizationResolver resolver,
            int commandId,
            string characterId,
            bool useOverride,
            string overrideSource,
            string bodySource)
        {
            // 表示文だけを解決し、音声照合用原文を独立して保持する
            // Resolve display text while preserving an independent source for voice lookup
            var displayBody = resolver.ResolveCommandField(commandId, BodyField, bodySource);
            var speakerName = useOverride
                ? resolver.ResolveOverriddenCharacterName(commandId, overrideSource)
                : resolver.ResolveCharacterName(characterId);
            return new ResolvedSkitLine(speakerName, displayBody, bodySource);
        }

        public static string ResolveOption1(
            ISkitLocalizationResolver resolver,
            int commandId,
            string sourceText)
        {
            return resolver.ResolveCommandField(commandId, Option1Field, sourceText);
        }

        public static string ResolveOption2(
            ISkitLocalizationResolver resolver,
            int commandId,
            string sourceText)
        {
            return resolver.ResolveCommandField(commandId, Option2Field, sourceText);
        }

        public static string ResolveOption3(
            ISkitLocalizationResolver resolver,
            int commandId,
            string sourceText)
        {
            return resolver.ResolveCommandField(commandId, Option3Field, sourceText);
        }
    }
}
