namespace Client.PlaytestReceiver.Delivery
{
    public abstract class PlaytestOutboxInventoryRead
    {
        private protected PlaytestOutboxInventoryRead() { }
    }

    public sealed class PlaytestOutboxDeliveryCounts : PlaytestOutboxInventoryRead
    {
        public readonly int PendingCount;
        public readonly int FailedCount;
        public PlaytestOutboxDeliveryCounts(int pendingCount, int failedCount)
        {
            PendingCount = pendingCount;
            FailedCount = failedCount;
        }
    }

    public sealed class PlaytestOutboxInventoryUnavailable : PlaytestOutboxInventoryRead
    {
        public readonly string Reason;
        public PlaytestOutboxInventoryUnavailable(string reason) { Reason = reason; }
    }
}
