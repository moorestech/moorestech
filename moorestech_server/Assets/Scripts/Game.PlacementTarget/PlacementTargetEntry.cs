using System;

namespace Game.PlacementTarget
{
    public readonly struct PlacementTargetEntry
    {
        public readonly Guid Id;
        public readonly PlacementTargetKind Kind;
        public readonly string DisplayName;

        public PlacementTargetEntry(Guid id, PlacementTargetKind kind, string displayName)
        {
            Id = id;
            Kind = kind;
            DisplayName = displayName;
        }
    }
}
