using Client.Game.InGame.BugReport.Playtest;

namespace Client.PlaytestReceiver.Gate
{
    // 起動時照合で受け口が検証したSteamIDを、報告・進行記録・異常終了箱の識別として差し込む実体（ADR 0065）
    // The identity that carries the SteamID the receiver verified at the launch check into reports, progress records and crash boxes (ADR 0065)
    public sealed class ReceiverVerifiedSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId { get; }

        public ReceiverVerifiedSessionIdentity(string steamId)
        {
            SteamId = steamId;
        }
    }
}
