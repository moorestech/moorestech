using System;
using Client.Ui.State.PlacementTarget;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー 1 枠の中身。未割当・割当はあるが解けない・解けた対象の 3 つ（Web の null・unresolved・解決済み）
    /// One hotbar slot's content: unassigned, assigned but unresolvable, or resolved (Web null, unresolved, resolved)
    /// </summary>
    public abstract class HotbarSlotViewModel
    {
        private protected HotbarSlotViewModel()
        {
        }

        // 割当があるか（未解決も割当あり。外せる・白面で出す）
        // Whether an assignment exists (unresolved counts: it can be cleared and shows the white face)
        public abstract bool IsAssigned { get; }
    }

    public sealed class HotbarEmptySlot : HotbarSlotViewModel
    {
        public static readonly HotbarEmptySlot Instance = new();

        private HotbarEmptySlot()
        {
        }

        public override bool IsAssigned => false;
    }

    public sealed class HotbarUnresolvedSlot : HotbarSlotViewModel
    {
        public Guid AssignedId { get; }

        public HotbarUnresolvedSlot(Guid assignedId)
        {
            AssignedId = assignedId;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarUnresolvedSlot other && other.AssignedId == AssignedId;
        public override int GetHashCode() => AssignedId.GetHashCode();
    }

    // 解けた対象。Screens が Client.Game の型を受けないよう、描くのに要る ID・表示名・絵の出どころだけを運ぶ
    // A resolved target; carries only the id, display name and picture source needed to draw, so Screens never take Client.Game types
    public sealed class HotbarAssignedSlot : HotbarSlotViewModel
    {
        public Guid TargetId { get; }
        public string DisplayLabel { get; }
        public PlacementTargetIcon Icon { get; }

        public HotbarAssignedSlot(Guid targetId, string displayLabel, PlacementTargetIcon icon)
        {
            TargetId = targetId;
            DisplayLabel = displayLabel;
            Icon = icon;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarAssignedSlot other && other.TargetId == TargetId && other.DisplayLabel == DisplayLabel && other.Icon.Equals(Icon);
        public override int GetHashCode() => TargetId.GetHashCode();
    }
}
