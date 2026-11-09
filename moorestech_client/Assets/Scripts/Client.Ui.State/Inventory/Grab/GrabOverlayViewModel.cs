using System;
using Core.Master;

namespace Client.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 掴み絵に描くもの（判別 union。隠す、または何を何個）。値で比べるので同じ表示は流れない（前例 ModeHudViewModel）
    /// What the grab picture draws (discriminated union: hidden, or which item and how many); compared by value so identical views are not republished (precedent: ModeHudViewModel)
    /// </summary>
    public abstract class GrabOverlayViewModel : IEquatable<GrabOverlayViewModel>
    {
        public static readonly GrabOverlayViewModel Hidden = new GrabOverlayHidden();

        public static GrabOverlayViewModel Holding(ItemId itemId, int count) => new GrabOverlayHolding(itemId, count);

        public abstract bool Equals(GrabOverlayViewModel other);
        public override bool Equals(object obj) => Equals(obj as GrabOverlayViewModel);
        public abstract override int GetHashCode();
    }

    // 隠す（掴みを扱わない画面か、何も持っていない）
    // Hidden (a screen without grab handling, or holding nothing)
    public sealed class GrabOverlayHidden : GrabOverlayViewModel
    {
        internal GrabOverlayHidden()
        {
        }

        public override bool Equals(GrabOverlayViewModel other) => other is GrabOverlayHidden;
        public override int GetHashCode() => 0;
        public override string ToString() => "Hidden";
    }

    // 持っているアイテムと個数を描く
    // Draws the held item and its count
    public sealed class GrabOverlayHolding : GrabOverlayViewModel
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        internal GrabOverlayHolding(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public override bool Equals(GrabOverlayViewModel other) => other is GrabOverlayHolding holding && ItemId == holding.ItemId && Count == holding.Count;
        public override int GetHashCode() => HashCode.Combine(ItemId, Count);
        public override string ToString() => $"Holding({ItemId}, {Count})";
    }
}
