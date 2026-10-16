namespace Client.PlaytestReceiver.Delivery
{
    public enum PlaytestOutboxScope { ReportsAndProgress, ReportsOnly }
    public enum PlaytestLocalOnlyReason { DeveloperTokenMissing, DeveloperTokenUnreadable, SteamNotRunning }
    public abstract class PlaytestDeliveryRoute { public abstract PlaytestOutboxScope Scope { get; } }
    public sealed class PlaytestSteamRoute : PlaytestDeliveryRoute
    {
        public override PlaytestOutboxScope Scope => PlaytestOutboxScope.ReportsAndProgress;
    }
    public sealed class PlaytestDeveloperTokenRoute : PlaytestDeliveryRoute
    {
        public readonly string DeveloperToken;
        public PlaytestDeveloperTokenRoute(string developerToken) { DeveloperToken = developerToken; }
        public override PlaytestOutboxScope Scope => PlaytestOutboxScope.ReportsOnly;
    }
    public sealed class PlaytestLocalOnlyRoute : PlaytestDeliveryRoute
    {
        public readonly PlaytestLocalOnlyReason Reason;
        public readonly string Detail;
        private readonly PlaytestOutboxScope _scope;
        public PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason reason, PlaytestOutboxScope scope, string detail)
        { Reason = reason; _scope = scope; Detail = detail; }
        public override PlaytestOutboxScope Scope => _scope;
    }
}
