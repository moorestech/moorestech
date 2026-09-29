namespace Client.Starter.Localization
{
    // Steamのゲーム個別言語の読み取り口
    // Read port for Steam's per-game language
    internal interface ISteamGameLanguageReader
    {
        bool TryRead(out string steamLanguage, out string failureReason);
    }
}
