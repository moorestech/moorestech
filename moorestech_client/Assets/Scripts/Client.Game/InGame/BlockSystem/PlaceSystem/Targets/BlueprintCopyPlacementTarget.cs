using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintCopyPlacementTarget : IPlacementTarget
    {
        // BPコピーツールのGuid。表示名はマスタから解決する
        // Guid of the blueprint copy tool; the display name is resolved from the master
        public Guid Id { get; }
        public PlacementTargetKind Kind => PlacementTargetKind.BlueprintCopy;
        public string DisplayName => MasterHolder.BuildToolMaster.GetBuildTool(Id).Name;

        public BlueprintCopyPlacementTarget(Guid blueprintCopyToolGuid)
        {
            Id = blueprintCopyToolGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlueprintCopyPlacementTarget target && target.Id == Id;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
