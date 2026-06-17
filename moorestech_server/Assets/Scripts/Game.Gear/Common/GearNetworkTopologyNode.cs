namespace Game.Gear.Common
{
    internal readonly struct GearNetworkTopologyNode
    {
        public readonly IGearEnergyTransformer Transformer;
        public readonly float RpmRatioFromRoot;
        public readonly bool IsClockwiseSameAsRoot;

        public GearNetworkTopologyNode(IGearEnergyTransformer transformer, float rpmRatioFromRoot, bool isClockwiseSameAsRoot)
        {
            Transformer = transformer;
            RpmRatioFromRoot = rpmRatioFromRoot;
            IsClockwiseSameAsRoot = isClockwiseSameAsRoot;
        }

        public bool GetClockwise(bool rootClockwise)
        {
            return IsClockwiseSameAsRoot ? rootClockwise : !rootClockwise;
        }

        public bool GetRootClockwise(bool currentClockwise)
        {
            return IsClockwiseSameAsRoot ? currentClockwise : !currentClockwise;
        }
    }
}
