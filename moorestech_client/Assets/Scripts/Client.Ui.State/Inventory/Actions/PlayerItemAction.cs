using System;
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// プレイヤー側の行為。種類ごとに型を分け、実行の中身は各型が持つ（外では派生できない）
    /// Player-side action; one type per kind, each owning its execution (cannot be derived outside)
    /// </summary>
    public abstract class PlayerItemAction
    {
        private protected PlayerItemAction()
        {
        }

        internal abstract UiCommandResult Run(IPlayerInventoryOperations operations);

        // 移動の結果を拒否理由へ（失敗の写しはここ 1 か所）
        // Maps a move outcome to a reject reason (the only place this mapping lives)
        private protected static UiCommandResult FromOutcome(PlayerItemMoveOutcome outcome)
        {
            return outcome switch
            {
                PlayerItemMoveOutcome.Moved => UiCommandAccepted.Instance,
                PlayerItemMoveOutcome.SourceEmpty => new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, ""),
                PlayerItemMoveOutcome.SourceCountShort => new UiCommandRejected(UiCommandRejectReason.ItemSourceCountShort, ""),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
            };
        }
    }

    public sealed class PickUpToGrabAction : PlayerItemAction
    {
        public PlayerSlotRef From { get; }
        public int Count { get; }

        public PickUpToGrabAction(PlayerSlotRef from, int count)
        {
            From = from;
            Count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations) => FromOutcome(operations.MoveToGrab(From, Count));
    }

    public sealed class PlaceFromGrabAction : PlayerItemAction
    {
        public PlayerSlotRef To { get; }
        public int Count { get; }

        public PlaceFromGrabAction(PlayerSlotRef to, int count)
        {
            To = to;
            Count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations) => FromOutcome(operations.MoveFromGrab(To, Count));
    }

    public sealed class MoveBetweenSlotsAction : PlayerItemAction
    {
        public PlayerSlotRef From { get; }
        public PlayerSlotRef To { get; }
        public int Count { get; }

        public MoveBetweenSlotsAction(PlayerSlotRef from, PlayerSlotRef to, int count)
        {
            From = from;
            To = to;
            Count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // silent-ok: 同じ枠への移動はホストと同じく成功の何もしない / 枠が変わらないことはプレイヤーが画面で見る
            if (From == To) return UiCommandAccepted.Instance;
            return FromOutcome(operations.MoveBetweenSlots(From, To, Count));
        }
    }

    public sealed class SplitToGrabAction : PlayerItemAction
    {
        public PlayerSlotRef From { get; }

        public SplitToGrabAction(PlayerSlotRef from)
        {
            From = from;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // grab を持っていれば拒否、中身が無ければ拒否、半分が 0 なら成功の何もしない（ホストと同じ順）
            // Reject while holding, reject an empty slot, and treat a zero half as a successful no-op (host order)
            var snapshot = operations.ReadSnapshot();
            if (!snapshot.Grab.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.SplitGrabNotEmpty, "");
            if (!snapshot.TryResolveSlot(From.ToItemMoveSlotRef(), out var slot))
            {
                Debug.LogWarning($"[PlayerItemAction] split on stale slot {From} ignored");
                return UiCommandAccepted.Instance;
            }
            if (slot.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, "");
            var half = slot.Count / 2;
            // silent-ok: 1 個の分割は半分が 0 でホストと同じ成功の何もしない / grab が空のままなのはプレイヤーが掴み絵の無いことで見る
            if (half <= 0) return UiCommandAccepted.Instance;
            return FromOutcome(operations.MoveToGrab(From, half));
        }
    }

    public sealed class CollectAction : PlayerItemAction
    {
        public PlayerSlotRef Clicked { get; }

        public CollectAction(PlayerSlotRef clicked)
        {
            Clicked = clicked;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // 集め先は今の grab で決める（ホストの ResolveCollectTarget と同じ）
            // The target follows the current grab (same as the host's ResolveCollectTarget)
            if (operations.ReadGrab().IsEmpty) operations.CollectToSlot(Clicked);
            else operations.CollectToGrab();
            return UiCommandAccepted.Instance;
        }
    }
}
