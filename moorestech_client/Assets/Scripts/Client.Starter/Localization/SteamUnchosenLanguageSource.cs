using Client.Localization;

namespace Client.Starter.Localization
{
    // Steam読取とCSV対応づけをLocalizeの出所にまとめる
    // Combine the Steam read and CSV mapping into a Localize source
    public sealed class SteamUnchosenLanguageSource : IUnchosenLanguageSource
    {
        public bool TryResolveGameLanguage(out string languageCode, out string failureReason)
        {
            languageCode = "";
            if (!SteamGameLanguageReader.TryRead(out var steamLanguage, out failureReason))
                return false;

            if (!SteamLanguageMapping.TryToGameLanguage(steamLanguage, out languageCode))
            {
                failureReason = $"Steam language {steamLanguage} has no mapping in localization_settings.csv";
                return false;
            }

            failureReason = "";
            return true;
        }
    }
}
