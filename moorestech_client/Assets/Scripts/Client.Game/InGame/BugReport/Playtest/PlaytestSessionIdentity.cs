namespace Client.Game.InGame.BugReport.Playtest
{
    // 報告に付けるテスター識別。実体は plan D の Steam 認証が入れ替える差込口
    // The tester identity attached to reports; plan D's Steam auth replaces this seam
    // 取れないときは空文字でなくnullを返す。空文字は「識別子が空の実テスター」という実値に化ける（F02）
    // It returns null rather than an empty string when unavailable; an empty string would pose as a real tester with a blank id (F02)
    public interface IPlaytestSessionIdentity
    {
        string SteamId { get; }
    }

    // 既定の実体（開発者のrsync経路）。SteamIDは存在しないのでnull
    // The default implementation (the developer rsync path); there is no SteamID, so it is null
    public sealed class EmptyPlaytestSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId => null;
    }
}
