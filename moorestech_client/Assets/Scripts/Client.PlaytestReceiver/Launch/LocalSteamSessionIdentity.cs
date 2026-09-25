using Client.Game.InGame.BugReport.Playtest;

namespace Client.PlaytestReceiver.Launch
{
    // 配布版の起動時にローカルSteamから読んだSteamIDを記録の識別として差し込む実体（ADR 0070）
    // Carries the local SteamID read at distribution startup into recorded identity (ADR 0070)
    internal sealed class LocalSteamSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId { get; }
        public string SteamIdAbsenceReason => null;

        public LocalSteamSessionIdentity(string steamId)
        {
            SteamId = steamId;
        }
    }
}
