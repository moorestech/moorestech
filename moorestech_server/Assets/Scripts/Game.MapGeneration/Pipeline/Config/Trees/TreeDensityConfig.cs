namespace Game.MapGeneration.Pipeline.Config
{
    // 樹木密度分布のバイオーム別パラメータ（Dense/Transition/Sparse の3層＋各種フィルタ）。
    // Per-biome tree density parameters (Dense/Transition/Sparse layers plus filters).
    public class TreeDensityConfig
    {
        public float denseMinThreshold = 0.45f;
        public float transitionMinThreshold = 0.26f;
        public float densePassMinDistance = 15.0f;
        public float transitionPassMinDistance = 8.0f;
        public float sparsePassMinDistance = 20f;
        public float scatterPassMinDistance = 22f;
        public float transitionBaseProb = 0.06f;
        public float transitionPeakProb = 0.74f;
        public float transitionProbPower = 1.5f;
        public float sparseOpenRejectFactor = 0.6f;
        public float scatterBaseProb = 0.02f;
        public float scatterDensityFactor = 0.25f;
        public float slopeHardReject = 30f;
        public float slopeSoftReject = 20f;
        public float rockRejectDistance = 5f;
        public float rockRejectProb = 0.9f;
        public float rockBoostNearDistance = 15f;
        public float rockBoostFarDistance = 25f;
        public float rockFarRejectProb = 0.5f;
        public float densityLargeFrequency = 0.007f;
        public float densityMidFrequency = 0.013f;
        public float densitySmallFrequency = 0.028f;
        public float densityLargeWeight = 0.40f;
        public float densityMidWeight = 0.40f;
        public float densitySmallWeight = 0.20f;
        public float densityFloor = 0f;
        public float islandModulationFrequency = 0.02f;
        public float islandModulationMin = 0.78f;
        public float islandModulationMax = 1.0f;
        public float canopyScaleThreshold = 1f;
        public float densePassMultiplier = 4f;
        public float transitionPassMultiplier = 1.05f;
        public float sparsePassMultiplier = 0.06f;
        public float densityModMin = 0.3f;
        public float densityModMax = 1.0f;
        public float densityModScale = 1.5f;
        public float keepProbNear = 0.95f;
        public float keepProbFar = 0.15f;
        public float localDensityCapRadius = 20f;
        public int localDensityCapCount = 8;
    }
}
