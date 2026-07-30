namespace Client.Skit.Localization
{
    public interface ISkitLocalizationResolver
    {
        string ResolveCommandField(
            string skitTitle,
            int commandId,
            string field,
            string sourceText);

        string ResolveCharacterName(
            string characterId,
            string skitTitle,
            int commandId,
            bool useOverride,
            string overrideSource);
    }
}
