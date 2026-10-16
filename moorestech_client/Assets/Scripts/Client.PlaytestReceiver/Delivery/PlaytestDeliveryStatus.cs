namespace Client.PlaytestReceiver.Delivery
{
    public abstract class PlaytestDeliveryStatus { }
    public sealed class PlaytestDeliveryLocalOnly : PlaytestDeliveryStatus
    {
        public readonly PlaytestLocalOnlyReason Reason;
        public readonly int PendingCount;
        public readonly int FailedCount;
        public PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason reason, int pendingCount, int failedCount) { Reason = reason; PendingCount = pendingCount; FailedCount = failedCount; }
    }
    public sealed class PlaytestDeliveryPending : PlaytestDeliveryStatus
    {
        public readonly int PendingCount;
        public readonly int FailedCount;
        public PlaytestDeliveryPending(int pendingCount, int failedCount) { PendingCount = pendingCount; FailedCount = failedCount; }
    }
    public sealed class PlaytestDeliveryRefused : PlaytestDeliveryStatus
    {
        public readonly int PendingCount;
        public readonly int FailedCount;
        public PlaytestDeliveryRefused(int pendingCount, int failedCount) { PendingCount = pendingCount; FailedCount = failedCount; }
    }
    public sealed class PlaytestDeliveryAllSent : PlaytestDeliveryStatus
    {
        public static readonly PlaytestDeliveryAllSent Instance = new();
    }
}
