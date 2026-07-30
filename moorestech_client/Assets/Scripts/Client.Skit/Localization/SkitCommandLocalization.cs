using Client.Skit.Context;

namespace Client.Skit.Localization
{
    public static class SkitCommandLocalization
    {
        public static ResolvedSkitLine ResolveLine(
            ISkitLocalizationResolver resolver,
            SkitExecutionIdentity identity,
            int commandId,
            string characterId,
            bool useOverride,
            string overrideSource,
            string bodySource)
        {
            // 表示文だけを解決し、音声照合用原文を独立して保持する
            // Resolve display text while preserving an independent source for voice lookup
            var displayBody = resolver.ResolveCommandField(
                identity.SkitTitle,
                commandId,
                "body",
                bodySource);
            var speakerName = resolver.ResolveCharacterName(
                characterId,
                identity.SkitTitle,
                commandId,
                useOverride,
                overrideSource);
            return new ResolvedSkitLine(speakerName, displayBody, bodySource);
        }

        public static string ResolveOption1(
            ISkitLocalizationResolver resolver,
            SkitExecutionIdentity identity,
            int commandId,
            string sourceText)
        {
            return ResolveOption(resolver, identity, commandId, "Option1Tag", sourceText);
        }

        public static string ResolveOption2(
            ISkitLocalizationResolver resolver,
            SkitExecutionIdentity identity,
            int commandId,
            string sourceText)
        {
            return ResolveOption(resolver, identity, commandId, "Option2Tag", sourceText);
        }

        public static string ResolveOption3(
            ISkitLocalizationResolver resolver,
            SkitExecutionIdentity identity,
            int commandId,
            string sourceText)
        {
            return ResolveOption(resolver, identity, commandId, "Option3Tag", sourceText);
        }

        private static string ResolveOption(
            ISkitLocalizationResolver resolver,
            SkitExecutionIdentity identity,
            int commandId,
            string field,
            string sourceText)
        {
            return resolver.ResolveCommandField(identity.SkitTitle, commandId, field, sourceText);
        }
    }
}
