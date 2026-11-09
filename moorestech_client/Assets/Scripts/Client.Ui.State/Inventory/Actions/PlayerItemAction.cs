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
                // 何もしない移動は Web と同じく成功（警告ログは持ち物の口が出す）
                // A no-effect move is accepted like Web (the inventory port logs the warning)
                PlayerItemMoveOutcome.NoEffect => UiCommandAccepted.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
            };
        }
    }

    public sealed class PickUpToGrabAction : PlayerItemAction
    {
        public PlayerSlotRef From { get; }
        private readonly int _count;

        public PickUpToGrabAction(PlayerSlotRef from, int count)
        {
            From = from;
            _count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations) => FromOutcome(operations.MoveToGrab(From, _count));
    }

    public sealed class PlaceFromGrabAction : PlayerItemAction
    {
        public PlayerSlotRef To { get; }
        private readonly int _count;

        public PlaceFromGrabAction(PlayerSlotRef to, int count)
        {
            To = to;
            _count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations) => FromOutcome(operations.MoveFromGrab(To, _count));
    }

    public sealed class MoveBetweenSlotsAction : PlayerItemAction
    {
        private readonly PlayerSlotRef _from;
        private readonly PlayerSlotRef _to;
        private readonly int _count;

        public MoveBetweenSlotsAction(PlayerSlotRef from, PlayerSlotRef to, int count)
        {
            _from = from;
            _to = to;
            _count = count;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // silent-ok: 同じ枠への移動はホストと同じく成功の何もしない / 枠が変わらないことはプレイヤーが画面で見る
            if (_from == _to) return UiCommandAccepted.Instance;
            return FromOutcome(operations.MoveBetweenSlots(_from, _to, _count));
        }
    }

    public sealed class SplitToGrabAction : PlayerItemAction
    {
        private readonly PlayerSlotRef _from;

        public SplitToGrabAction(PlayerSlotRef from)
        {
            _from = from;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // grab を持っていれば拒否、中身が無ければ拒否、半分が 0 なら成功の何もしない（ホストと同じ順）
            // Reject while holding, reject an empty slot, and treat a zero half as a successful no-op (host order)
            var snapshot = operations.ReadSnapshot();
            if (!snapshot.Grab.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.SplitGrabNotEmpty, "");
            if (!snapshot.TryResolveSlot(_from.ToItemMoveSlotRef(), out var slot))
            {
                Debug.LogWarning($"[PlayerItemAction] split on stale slot {_from} ignored");
                return UiCommandAccepted.Instance;
            }
            if (slot.IsEmpty) return new UiCommandRejected(UiCommandRejectReason.ItemSourceSlotEmpty, "");
            var half = slot.Count / 2;
            // silent-ok: 1 個の分割は半分が 0 でホストと同じ成功の何もしない / grab が空のままなのはプレイヤーが掴み絵の無いことで見る
            if (half <= 0) return UiCommandAccepted.Instance;
            return FromOutcome(operations.MoveToGrab(_from, half));
        }
    }

    public sealed class CollectAction : PlayerItemAction
    {
        private readonly PlayerSlotRef _clicked;

        public CollectAction(PlayerSlotRef clicked)
        {
            _clicked = clicked;
        }

        internal override UiCommandResult Run(IPlayerInventoryOperations operations)
        {
            // 集め先は今の grab で決める（ホストの ResolveCollectTarget と同じ）
            // The target follows the current grab (same as the host's ResolveCollectTarget)
            if (operations.ReadGrab().IsEmpty) operations.CollectToSlot(_clicked);
            else operations.CollectToGrab();
            return UiCommandAccepted.Instance;
        }
    }
}
