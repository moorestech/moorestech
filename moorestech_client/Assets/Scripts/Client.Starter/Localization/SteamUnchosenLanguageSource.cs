using Client.Localization;
using UnityEngine;

namespace Client.Starter.Localization
{
    // SteamとCSVをLocalize出所に統合
    // Combine the Steam read and CSV mapping into a Localize source
    internal sealed class SteamUnchosenLanguageSource : IUnchosenLanguageSource
    {
        private readonly ISteamGameLanguageReader reader;

        public SteamUnchosenLanguageSource(ISteamGameLanguageReader reader)
        {
            this.reader = reader;
        }

        public bool TryResolveGameLanguage(out string languageCode, out string failureReason)
        {
            languageCode = "";

            // EditorはSteamを読まない
            // The editor never consults Steam
            if (Application.isEditor)
            {
                failureReason = "editor session does not consult Steam";
                return false;
            }

            if (!reader.TryRead(out var steamLanguage, out failureReason))
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
