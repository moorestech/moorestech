namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    // 鉱脈AABBと採掘底面・整地面を1組で運ぶ
    // Carries the vein AABB with its mining bottom and pad
    public readonly struct VeinGrounding
    {
        public readonly PlacedVein Vein;
        public readonly int Bottom;
        public readonly VeinGroundingPad Pad;

        public VeinGrounding(PlacedVein vein, int bottom, VeinGroundingPad pad)
        {
            Vein = vein;
            Bottom = bottom;
            Pad = pad;
        }
    }
}
