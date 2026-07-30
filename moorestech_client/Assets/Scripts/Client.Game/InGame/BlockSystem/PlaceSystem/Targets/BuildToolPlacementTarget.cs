using System;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BuildToolPlacementTarget : IPlacementTarget
    {
        // 選択されたbuildToolのGuid。種別・表示名はマスタから解決する
        // Guid of the selected buildTool; type and display name are resolved from the master
        public Guid Id { get; }
        public PlacementTargetKind Kind => PlacementTargetKind.BuildTool;

        public BuildToolPlacementTarget(Guid buildToolGuid)
        {
            Id = buildToolGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BuildToolPlacementTarget target && target.Id == Id;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
