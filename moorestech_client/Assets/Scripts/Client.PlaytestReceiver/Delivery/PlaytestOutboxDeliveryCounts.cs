namespace Client.PlaytestReceiver.Delivery
{
    public sealed class PlaytestOutboxDeliveryCounts
    {
        public readonly int PendingCount;
        public readonly int FailedCount;

        public PlaytestOutboxDeliveryCounts(int pendingCount, int failedCount)
        {
            PendingCount = pendingCount;
            FailedCount = failedCount;
        }
    }
}
