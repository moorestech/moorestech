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

        // 割当の有無（未解決も有り）
        // Whether an assignment exists (unresolved counts)
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
        private readonly Guid _assignedId;

        public HotbarUnresolvedSlot(Guid assignedId)
        {
            _assignedId = assignedId;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarUnresolvedSlot other && other._assignedId == _assignedId;
        public override int GetHashCode() => _assignedId.GetHashCode();
    }

    // 解けた対象。Screens が Client.Game の型を受けないよう、描くのに要る ID・表示名・絵の出どころだけを運ぶ
    // A resolved target; carries only the id, display name and picture source needed to draw, so Screens never take Client.Game types
    public sealed class HotbarAssignedSlot : HotbarSlotViewModel
    {
        private readonly Guid _targetId;
        public string DisplayLabel { get; }
        public PlacementTargetIcon Icon { get; }

        public HotbarAssignedSlot(Guid targetId, string displayLabel, PlacementTargetIcon icon)
        {
            _targetId = targetId;
            DisplayLabel = displayLabel;
            Icon = icon;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarAssignedSlot other && other._targetId == _targetId && other.DisplayLabel == DisplayLabel && other.Icon.Equals(Icon);
        public override int GetHashCode() => _targetId.GetHashCode();
    }
}
