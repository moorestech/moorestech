namespace Client.PlaytestReceiver.Delivery
{
    public abstract class PlaytestDeliveryStatus { }
    public sealed class PlaytestDeliveryLocalOnly : PlaytestDeliveryStatus
    {
        public readonly PlaytestLocalOnlyReason Reason;
        public readonly PlaytestOutboxInventoryRead Inventory;
        public PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason reason, PlaytestOutboxInventoryRead inventory) { Reason = reason; Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryPending : PlaytestDeliveryStatus
    {
        public readonly PlaytestOutboxInventoryRead Inventory;
        public PlaytestDeliveryPending(PlaytestOutboxInventoryRead inventory) { Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryRefused : PlaytestDeliveryStatus
    {
        public readonly PlaytestOutboxInventoryRead Inventory;
        public PlaytestDeliveryRefused(PlaytestOutboxInventoryRead inventory) { Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryAllSent : PlaytestDeliveryStatus
    {
        public static readonly PlaytestDeliveryAllSent Instance = new();
    }
}
