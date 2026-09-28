using System;
using Steamworks;

namespace Client.Starter.Localization
{
    // Steamのゲーム個別言語を読むネイティブ境界
    // Native boundary for Steam's per-game language
    public static class SteamGameLanguageReader
    {
        public static bool TryRead(out string steamLanguage, out string failureReason)
        {
            // Steamクライアント（外部プロセス）とのネイティブ通信境界。未初期化・dll不在の例外を理由に変換する
            // Native IPC boundary to the Steam client process; convert uninitialized/missing-dll exceptions into a reason
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
