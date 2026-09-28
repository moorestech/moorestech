using Mooresmaster.Localization.Generated;

namespace Client.Starter.Localization
{
    // CSVの対応づけでSteam言語をゲーム言語に寄せる
    // Map Steam languages to game languages through the CSV catalog
    internal static class SteamLanguageMapping
    {
        public static bool TryToGameLanguage(string steamLanguage, out string gameLanguage)
        {
            foreach (var language in LanguageCatalog.Languages)
            {
                foreach (var candidate in language.SteamLanguages)
                {
                    if (candidate != steamLanguage) continue;
                    gameLanguage = language.Code;
                    return true;
                }
            }

            gameLanguage = "";
            return false;
        }
    }
}
