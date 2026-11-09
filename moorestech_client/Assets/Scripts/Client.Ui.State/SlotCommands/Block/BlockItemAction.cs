using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側の行為。種類ごとに型を分け、範囲判定と実行の中身は各型が持つ（外では派生できない）。規則は Web ホスト BlockInventoryActions と同じ [A-93]
    /// Block-side action; one type per kind, each owning its range check and execution (cannot be derived outside); rules match the Web host BlockInventoryActions [A-93]
    /// </summary>
    public abstract class BlockItemAction
    {
        private protected BlockItemAction()
        {
        }

        internal abstract bool IsInRange(IReadOnlyList<SlotItem> slots);
        internal abstract UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty);

        private protected static bool InRange(IReadOnlyList<SlotItem> slots, ItemMoveSlotRef slot)
        {
            return slot.Area != ItemMoveArea.Block || BlockSlotIndex.IsInRange(slots, slot.Slot);
        }

        private protected static UiCommandResult FromOutcome(PlayerItemMoveOutcome outcome) => PlayerItemMoveOutcomeConversion.ToCommandResult(outcome);
    }

    public sealed class BlockMoveItemAction : BlockItemAction
    {
        private readonly ItemMoveSlotRef _from;
        private readonly ItemMoveSlotRef _to;
        private readonly int _count;

        public BlockMoveItemAction(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            _from = from;
            _to = to;
            _count = count;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => InRange(slots, _from) && InRange(slots, _to);
        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty) => FromOutcome(block.Move(_from, _to, _count));
        public override string ToString() => $"BlockMoveItemAction({_from} -> {_to} x{_count})";
    }

    public sealed class BlockSplitToGrabAction : BlockItemAction
    {
        private readonly int _blockSlot;

        public BlockSplitToGrabAction(int blockSlot)
        {
            _blockSlot = blockSlot;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => BlockSlotIndex.IsInRange(slots, _blockSlot);
        public override string ToString() => $"BlockSplitToGrabAction(slot={_blockSlot})";

        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty)
        {
            // 半分の計算はホストの SplitGrabActionHandler と同じ床関数。1 個以下は成功扱いの何もしない
            // Half uses the same floor as the host; a stack of one or less is a successful no-op
            if (!grabEmpty) return new UiCommandRejected(UiCommandRejectReason.SplitGrabNotEmpty, "");
            var slot = slots[_blockSlot];
            if (slot.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, "");
            var half = slot.Count / 2;
            // silent-ok: 1 個の枠の右押下は Web と同じく何もしない / 枠に 1 個残っているのがプレイヤーに見えている
            if (half == 0) return UiCommandAccepted.Instance;
            return FromOutcome(block.Move(ItemMoveSlotRef.Block(_blockSlot), ItemMoveSlotRef.Grab, half));
        }
    }

    public sealed class BlockCollectAction : BlockItemAction
    {
        private readonly int _blockSlot;

        public BlockCollectAction(int blockSlot)
        {
            _blockSlot = blockSlot;
        }

        internal override bool IsInRange(IReadOnlyList<SlotItem> slots) => BlockSlotIndex.IsInRange(slots, _blockSlot);
        public override string ToString() => $"BlockCollectAction(slot={_blockSlot})";

        internal override UiCommandResult Run(IBlockInventoryOperations block, IReadOnlyList<SlotItem> slots, bool grabEmpty)
        {
            // 集める先は操作の瞬間の持ち手で決める（ホストの ResolveCollectTarget と同じ）
            // The target follows the grab at gesture time (same as the host ResolveCollectTarget)
            if (grabEmpty) block.CollectToBlockSlot(_blockSlot);
            else block.CollectToGrab();
            return UiCommandAccepted.Instance;
        }
    }
}
