namespace Client.Localization
{
    // 未選択時だけ参照する言語の出所を表す
    // Represents a language source consulted only before the player chooses
    public interface IUnchosenLanguageSource
    {
        bool TryResolveGameLanguage(out string languageCode, out string failureReason);
    }
}
