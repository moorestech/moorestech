namespace Client.PlaytestReceiver.Delivery
{
    public abstract class PlaytestDeliveryStatus { }
    // 在庫の読みを持つ配送状態の共通口。読み手が具体型を列挙しないための正本
    // Common surface of delivery states carrying an inventory read, so readers need not enumerate concrete types
    public interface IPlaytestDeliveryWithInventory
    {
        PlaytestOutboxInventoryRead Inventory { get; }
    }
    public sealed class PlaytestDeliveryLocalOnly : PlaytestDeliveryStatus, IPlaytestDeliveryWithInventory
    {
        public readonly PlaytestLocalOnlyReason Reason;
        public PlaytestOutboxInventoryRead Inventory { get; }
        public PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason reason, PlaytestOutboxInventoryRead inventory) { Reason = reason; Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryPending : PlaytestDeliveryStatus, IPlaytestDeliveryWithInventory
    {
        public PlaytestOutboxInventoryRead Inventory { get; }
        public PlaytestDeliveryPending(PlaytestOutboxInventoryRead inventory) { Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryRefused : PlaytestDeliveryStatus, IPlaytestDeliveryWithInventory
    {
        public PlaytestOutboxInventoryRead Inventory { get; }
        public PlaytestDeliveryRefused(PlaytestOutboxInventoryRead inventory) { Inventory = inventory; }
    }
    public sealed class PlaytestDeliveryAllSent : PlaytestDeliveryStatus
    {
        public static readonly PlaytestDeliveryAllSent Instance = new();
    }
}
