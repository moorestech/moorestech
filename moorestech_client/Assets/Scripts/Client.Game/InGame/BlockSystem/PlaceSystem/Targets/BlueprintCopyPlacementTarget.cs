using System;
using System.Collections.Generic;
using Client.Localization;
using Game.PlacementTarget;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlueprintCopyPlacementTarget : IPlacementTarget
    {
        public Guid Id { get; }
        public PlacementTargetKind Kind => PlacementTargetKind.BlueprintCopy;
        public string DisplayName => Localize.Get(LocalizationKeys.Ui.BuildMenu.BlueprintCopy);

        public IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems() => Array.Empty<(Guid, int)>();

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
