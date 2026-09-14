namespace Client.PlaytestReceiver
{
    // 読み取り面。トークンの取得・更新はアセンブリ内部（PlaytestSession）だけが行う
    // Read-only face; acquiring and refreshing the token stays inside this assembly
    public interface IPlaytestSessionLookup
    {
        string SteamId { get; }
        bool HasToken { get; }
    }
}
