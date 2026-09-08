using System;

namespace Game.PlacementTarget
{
    public readonly struct PlacementTargetEntry
    {
        public readonly Guid Id;
        public readonly PlacementTargetKind Kind;
        public readonly string MasterDisplayName;

        // 解放状態を決めるGuid。自分自身とは限らない（坂ベルトは直線へ寄る）
        // The guid whose unlock state governs this entry; not always its own id (belt slopes point at the straight block)
        public readonly Guid UnlockSourceId;

        public PlacementTargetEntry(Guid id, PlacementTargetKind kind, string masterDisplayName, Guid unlockSourceId)
        {
            Id = id;
            Kind = kind;
            MasterDisplayName = masterDisplayName;
            UnlockSourceId = unlockSourceId;
        }
    }
}
