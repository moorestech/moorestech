using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー全体の表示状態。値が同じなら等しく、ReactiveProperty が描き直しを省く
    /// The whole hotbar display state; equal values compare equal so ReactiveProperty skips redraws
    /// </summary>
    public sealed class HotbarViewModel : IEquatable<HotbarViewModel>
    {
        public IReadOnlyList<HotbarSlotViewModel> Slots { get; }
        public HotbarSelection Selection { get; }
        public bool SelectAccepted { get; }
        public bool Visible { get; }

        public HotbarViewModel(IReadOnlyList<HotbarSlotViewModel> slots, HotbarSelection selection, bool selectAccepted, bool visible)
        {
            Slots = slots;
            Selection = selection;
            SelectAccepted = selectAccepted;
            Visible = visible;
        }

        public bool Equals(HotbarViewModel other)
        {
            if (other == null) return false;
            return Selection.Equals(other.Selection) && SelectAccepted == other.SelectAccepted && Visible == other.Visible && Slots.SequenceEqual(other.Slots);
        }

        public override bool Equals(object obj) => obj is HotbarViewModel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Selection, SelectAccepted, Visible, Slots.Count);
    }
}
