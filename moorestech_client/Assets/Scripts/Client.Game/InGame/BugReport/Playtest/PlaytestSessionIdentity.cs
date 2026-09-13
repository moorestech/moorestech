namespace Client.Game.InGame.BugReport.Playtest
{
    // 報告に付けるテスター識別。実体は plan D の Steam 認証が入れ替える差込口で、既定は空文字（開発者のrsync経路）
    // The tester identity attached to reports; plan D's Steam auth replaces this seam, and the default is empty (developer rsync path)
    public interface IPlaytestSessionIdentity
    {
        string SteamId { get; }
    }

    public sealed class EmptyPlaytestSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId => "";
    }
}
