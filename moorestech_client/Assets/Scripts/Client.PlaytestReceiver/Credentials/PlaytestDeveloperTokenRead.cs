namespace Client.PlaytestReceiver.Credentials
{
    public abstract class PlaytestDeveloperTokenRead { internal PlaytestDeveloperTokenRead() { } }
    public sealed class PlaytestDeveloperTokenPresent : PlaytestDeveloperTokenRead
    {
        public readonly string Token;
        public PlaytestDeveloperTokenPresent(string token) { Token = token; }
    }
    public sealed class PlaytestDeveloperTokenAbsent : PlaytestDeveloperTokenRead { }
    public sealed class PlaytestDeveloperTokenUnreadable : PlaytestDeveloperTokenRead
    {
        public readonly string Reason;
        public PlaytestDeveloperTokenUnreadable(string reason) { Reason = reason; }
    }
}
