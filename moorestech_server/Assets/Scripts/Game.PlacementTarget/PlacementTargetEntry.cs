using System;

namespace Game.PlacementTarget
{
    public readonly struct PlacementTargetEntry
    {
        public readonly Guid Id;
        public readonly PlacementTargetKind Kind;
        public readonly string MasterDisplayName;

        public PlacementTargetEntry(Guid id, PlacementTargetKind kind, string masterDisplayName)
        {
            Id = id;
            Kind = kind;
            MasterDisplayName = masterDisplayName;
        }
    }
}
