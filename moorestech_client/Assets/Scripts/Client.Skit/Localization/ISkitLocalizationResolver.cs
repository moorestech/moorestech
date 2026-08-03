namespace Client.Skit.Localization
{
    public interface ISkitLocalizationResolver
    {
        string ResolveCommandField(int commandId, string field, string sourceText);

        string ResolveCharacterName(string characterId);

        string ResolveOverriddenCharacterName(int commandId, string overrideSource);
    }
}
