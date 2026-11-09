using System;
using System.Collections.Generic;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// Shift 配分を送らずに断る理由。通知とログの文を選ぶのは呼び出し側
    /// Why a Shift allocation is refused unsent; the caller picks the notification and log text
    /// </summary>
    public enum ShiftMoveRefusalReason
    {
        NoAcceptingBlockSlot,
        InventoryOpenFailed,
        DetailLoading,
        DestinationFull,
        UnknownItem,
    }

    /// <summary>
    /// プレイヤースロット操作の帰結。Web の PlayerLeftClickPlan に slotActions の「古い番地の無視」を足した union
    /// Outcome of a player-slot gesture: the Web PlayerLeftClickPlan plus slotActions' stale-ref ignore
    /// </summary>
    public abstract class PlayerSlotActionPlan
    {
        private protected PlayerSlotActionPlan()
        {
        }
    }

    // 送る操作の列。空なら何も送らない
    // Actions to send; empty means nothing is sent
    public sealed class ItemActionsPlan : PlayerSlotActionPlan
    {
        public static readonly ItemActionsPlan Empty = new(Array.Empty<PlannedItemAction>());

        public IReadOnlyList<PlannedItemAction> Actions { get; }

        public ItemActionsPlan(IReadOnlyList<PlannedItemAction> actions)
        {
            Actions = actions;
        }
    }

    // 掴み中に空スロットを押した。分割ドラッグを始める
    // Pressed an empty slot while holding a grab; start a split drag
    public sealed class BeginSplitDragPlan : PlayerSlotActionPlan
    {
        public ItemMoveSlotRef Target { get; }

        public BeginSplitDragPlan(ItemMoveSlotRef target)
        {
            Target = target;
        }
    }

    // 左ボタンのままスロットへ入った。分割ドラッグの行き先に足す
    // Entered a slot with the left button held; add it to the split-drag destinations
    public sealed class EnterSplitDragPlan : PlayerSlotActionPlan
    {
        public ItemMoveSlotRef Target { get; }

        public EnterSplitDragPlan(ItemMoveSlotRef target)
        {
            Target = target;
        }
    }

    // Shift 配分を送らずに断った
    // Refused the Shift allocation without sending anything
    public sealed class ShiftMoveRefusedPlan : PlayerSlotActionPlan
    {
        public ShiftMoveRefusalReason Reason { get; }

        public ShiftMoveRefusedPlan(ShiftMoveRefusalReason reason)
        {
            Reason = reason;
        }
    }

    // 枠数が縮んだ直後の古い番地。対象が無いので何もしない（呼び出し側が理由をログへ出す）
    // A stale ref right after the slot count shrank; nothing to act on (the caller logs why)
    public sealed class StaleSlotIgnoredPlan : PlayerSlotActionPlan
    {
        public ItemMoveSlotRef Target { get; }

        public StaleSlotIgnoredPlan(ItemMoveSlotRef target)
        {
            Target = target;
        }
    }
}
