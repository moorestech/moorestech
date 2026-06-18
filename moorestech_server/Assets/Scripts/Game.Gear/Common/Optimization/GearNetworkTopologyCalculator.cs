namespace Game.Gear.Common
{
    internal static class GearNetworkTopologyCalculator
    {
        public static GearNetworkTopologyNode CalculateSourceNode(GearNetworkTopologyNode targetNode, IGearEnergyTransformer source, GearConnect connect)
        {
            var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
            var sourceRatio = CalculateSourceRatio(targetNode, source, connect, isReverseRotation);
            var sourceClockwiseSameAsRoot = isReverseRotation ? !targetNode.IsClockwiseSameAsRoot : targetNode.IsClockwiseSameAsRoot;
            return new GearNetworkTopologyNode(source, sourceRatio, sourceClockwiseSameAsRoot);
        }

        public static float CalculateTargetRatio(GearNetworkTopologyNode currentNode, GearConnect connect, bool isReverseRotation)
        {
            if (connect.Transformer is IGear targetGear && currentNode.Transformer is IGear currentGear && isReverseRotation)
                return currentNode.RpmRatioFromRoot * currentGear.TeethCount / targetGear.TeethCount;
            return currentNode.RpmRatioFromRoot;
        }

        private static float CalculateSourceRatio(GearNetworkTopologyNode targetNode, IGearEnergyTransformer source, GearConnect connect, bool isReverseRotation)
        {
            if (connect.Transformer is IGear targetGear && source is IGear sourceGear && isReverseRotation)
                return targetNode.RpmRatioFromRoot * targetGear.TeethCount / sourceGear.TeethCount;
            return targetNode.RpmRatioFromRoot;
        }
    }
}
