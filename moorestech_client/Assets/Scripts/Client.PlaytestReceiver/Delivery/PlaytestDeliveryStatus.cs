namespace Client.PlaytestReceiver.Delivery
{
    public abstract class PlaytestDeliveryStatus { }
    public sealed class PlaytestDeliveryLocalOnly : PlaytestDeliveryStatus
    {
        public readonly PlaytestLocalOnlyReason Reason;
        public readonly int PendingCount;
        public PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason reason, int pendingCount) { Reason = reason; PendingCount = pendingCount; }
    }
    public sealed class PlaytestDeliveryPending : PlaytestDeliveryStatus
    {
        public readonly int PendingCount;
        public PlaytestDeliveryPending(int pendingCount) { PendingCount = pendingCount; }
    }
    public sealed class PlaytestDeliveryRefused : PlaytestDeliveryStatus
    {
        public readonly int PendingCount;
        public PlaytestDeliveryRefused(int pendingCount) { PendingCount = pendingCount; }
    }
    public sealed class PlaytestDeliveryAllSent : PlaytestDeliveryStatus
    {
        public static readonly PlaytestDeliveryAllSent Instance = new();
    }
}
