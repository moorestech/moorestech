using Core.Master;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    // 継承元スロットが品をレベルの都合で受け取れなかった理由
    // Why the inheritance source slot could not take an item because of its level
    public enum MachineInputLevelRefusalReason
    {
        // スロットに別レベルの継承元が入っている
        // The slot holds a source of another level
        MixedLevel,

        // 製作上限を超えるレベル
        // A level above the craft cap
        AboveCraftCap,
    }

    /// <summary>
    ///     ブロックからの搬入が継承元のレベルで止まっている状態（理由と、届いて待っている品）
    ///     Inflow from blocks stalled on the inheritance source level (the reason and the item that arrived and waits)
    /// </summary>
    public readonly struct MachineInputLevelStall
    {
        public MachineInputLevelRefusalReason Reason { get; }
        public ItemId WaitingItemId { get; }

        public MachineInputLevelStall(MachineInputLevelRefusalReason reason, ItemId waitingItemId)
        {
            Reason = reason;
            WaitingItemId = waitingItemId;
        }
    }
}
