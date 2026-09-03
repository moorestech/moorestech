namespace Game.MapGeneration.Pipeline.Tiling
{
    // 1種別ぶんの鉱脈halo。種をまく先とcommit先を1個の参照に束ね、取り違えを型で塞ぐ。
    // One vein kind's halo; bundling the seeded and committed channels into a single reference makes a mismatch impossible.
    public class VeinHaloChannels
    {
        public readonly PlacementHaloChannel Members;
        public readonly PlacementHaloChannelMap Centers;

        public VeinHaloChannels(PlacementHaloChannel members, PlacementHaloChannelMap centers)
        {
            Members = members;
            Centers = centers;
        }
    }
}
