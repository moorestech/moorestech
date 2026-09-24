namespace Client.Game.InGame.BugReport.Playtest
{
    // 報告に付けるテスター識別。実体は plan D の Steam 認証が入れ替える差込口
    // The tester identity attached to reports; plan D's Steam auth replaces this seam
    // 取れないときは空文字でなくnullを返す。空文字は「識別子が空の実テスター」という実値に化ける（F02）
    // It returns null rather than an empty string when unavailable; an empty string would pose as a real tester with a blank id (F02)
    public interface IPlaytestSessionIdentity
    {
        string SteamId { get; }

        // SteamIDが無い理由。欠損列へそのまま載る。SteamIDを持つ実体はnullを返す
        // Why there is no SteamID, copied verbatim into the missing list; an identity that has one returns null
        string SteamIdAbsenceReason { get; }
    }

    // SteamIDを持たない実体。空になった事情は差し込む側が一番よく知っているので、理由は生成時に受け取る
    // The identity without a SteamID; whoever installs it knows best why it is empty, so the reason is taken at construction
    public sealed class EmptyPlaytestSessionIdentity : IPlaytestSessionIdentity
    {
        public const string DeveloperModeReason = "テスター識別（SteamID）が無い（開発者モード。build-info.json 無し、または Steam 未起動）";

        public string SteamId => null;
        public string SteamIdAbsenceReason { get; }

        public EmptyPlaytestSessionIdentity(string steamIdAbsenceReason)
        {
            SteamIdAbsenceReason = steamIdAbsenceReason;
        }
    }
}
