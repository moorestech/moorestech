using System;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintPlacementTarget : IPlacementTarget
    {
        // 識別はGuid、表示は名前（同名複数を区別するためGuidを一意キーとする）
        // Identity is the GUID; the name is display-only (a GUID uniquely distinguishes same-name entries)
        public readonly Guid BlueprintGuid;
        public readonly string DisplayName;

        public Guid Id => BlueprintGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.Blueprint;

        public BlueprintPlacementTarget(Guid blueprintGuid, string displayName)
        {
            BlueprintGuid = blueprintGuid;
            DisplayName = displayName;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlueprintPlacementTarget target && BlueprintGuid == target.BlueprintGuid;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => BlueprintGuid.GetHashCode();
    }
}
