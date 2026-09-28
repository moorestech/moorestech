using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Localization
{
    // CSVの対応づけでSteam言語をゲーム言語に寄せる
    // Map Steam languages to game languages through the CSV catalog
    public static class SteamLanguageMapping
    {
        public static string ToGameLanguage(string steamLanguage)
        {
            foreach (var language in LanguageCatalog.Languages)
            {
                foreach (var candidate in language.SteamLanguages)
                {
                    if (candidate == steamLanguage) return language.Code;
                }
            }

            return Localize.DefaultLanguageCode;
        }
    }
}
