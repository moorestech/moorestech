namespace Game.MapGeneration.Pipeline.Config
{
    // 下層木クラスタリング・独立散布のバイオーム別設定。
    // Per-biome understory clustering and independent scatter config.
    public class UnderstoryConfig
    {
        public float understoryScaleThreshold = 0.80f;
        public float understoryNeighborRadius = 2.0f;
        public int densePatches = 2;
        public int densePatchesRandom = 2;
        public int transitionPatches = 1;
        public int transitionPatchesRandom = 2;
        public int denseTreesPerCanopy = 6;
        public int denseTreesRandom = 5;
        public int transitionTreesPerCanopy = 4;
        public int transitionTreesRandom = 3;
        public float patchDistanceMin = 5f;
        public float patchDistanceMax = 12f;
        public float densePatchRadiusMin = 3.0f;
        public float densePatchRadiusMax = 4.5f;
        public float transitionPatchRadiusMin = 2.5f;
        public float transitionPatchRadiusMax = 3.8f;
        public float denseMaskThreshold = 0.34f;
        public float transitionMaskThreshold = 0.41f;
        public float understorySlopeLimit = 24f;
        public float scatterMinDistance = 20f;
        public float scatterDensityMultiplier = 0.7f;
        public float scatterProbMin = 0.25f;
        public float scatterProbMax = 0.60f;
        public float scatterSlopeLimit = 25f;
        public int scatterClusterSize = 6;
        public int scatterClusterSizeRandom = 10;
        public float scatterClusterRadiusMin = 5f;
        public float scatterClusterRadiusRandom = 5f;
        public float scatterNeighborRadius = 1.5f;
        public float patchAspectMin = 0.7f;
        public float patchAspectMax = 1.15f;
        public float scatterAspectMin = 0.6f;
        public float scatterAspectMax = 1.0f;
        public float patchMaskFrequency = 0.18f;
        public float patchMaskWeight = 0.75f;
        public float patchMaskEllipseOffset = 0.18f;
        public int patchTargetDense = 4;
        public int patchTargetDenseRandom = 3;
        public int patchTargetTransition = 3;
        public int patchTargetTransitionRandom = 3;
        public float edgeScaleMax = 1.1f;
        public float edgeScaleMin = 0.95f;
        public float scatterMaskFrequency = 0.12f;
        public float scatterMaskBlendMin = 0.5f;
        public float scatterMaskBlendMax = 1.0f;
    }
}
