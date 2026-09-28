using System;
using Steamworks;

namespace Client.Starter.Localization
{
    public interface ISteamGameLanguageReader
    {
        bool TryRead(out string steamLanguage, out string failureReason);
    }

    // Steamのゲーム個別言語を読むネイティブ境界
    // Native boundary for Steam's per-game language
    public sealed class SteamGameLanguageReader : ISteamGameLanguageReader
    {
        public bool TryRead(out string steamLanguage, out string failureReason)
        {
            // Steam未初期化やdll不在は外部境界の例外として理由に変換する
            // Convert native failures from uninitialized Steam or missing dll into a reason
            try
            {
                steamLanguage = SteamApps.GetCurrentGameLanguage();
            }
            catch (Exception exception)
            {
                steamLanguage = "";
                failureReason = $"SteamApps.GetCurrentGameLanguage failed: {exception.GetBaseException().Message}";
                return false;
            }

            if (string.IsNullOrEmpty(steamLanguage))
            {
                failureReason = "SteamApps.GetCurrentGameLanguage returned an empty language";
                return false;
            }

            failureReason = "";
            return true;
        }
    }
}
