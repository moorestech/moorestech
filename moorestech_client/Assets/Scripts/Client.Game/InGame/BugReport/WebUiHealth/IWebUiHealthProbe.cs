namespace Client.Game.InGame.BugReport.WebUiHealth
{
    // バグ報告がWebホスト・CEF・WS受信の生の値を読む窓口。実装はClient.WebUiHostにあり、Client.Gameはそれを参照しない
    // The window through which a bug report reads raw web host, CEF and WS receive values; the implementation lives in Client.WebUiHost, which Client.Game never references
    public interface IWebUiHealthProbe
    {
        WebUiHealthReading Read();
    }
}
