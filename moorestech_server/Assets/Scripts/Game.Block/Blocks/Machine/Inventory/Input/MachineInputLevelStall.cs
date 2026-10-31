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
    ///     - 継承元レベルによる搬入停止
    ///     - 停止理由と待機アイテム
    ///     - Inflow stalled by the source level, with the reason and waiting item
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
