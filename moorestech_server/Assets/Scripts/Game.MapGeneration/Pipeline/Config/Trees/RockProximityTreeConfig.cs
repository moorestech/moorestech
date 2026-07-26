namespace Game.MapGeneration.Pipeline.Config
{
    // 岩クラスター周辺に配置する樹木パッチのバイオーム別設定。
    // Per-biome config for tree patches placed around rock clusters.
    public class RockProximityTreeConfig
    {
        public bool enabled = true;
        public int patchCountMin = 1;
        public int patchCountRandom = 2;
        public float patchDistanceMin = 8f;
        public float patchDistanceRandom = 6f;
        public float patchSizeMin = 12f;
        public float patchSizeRandom = 6f;
        public float maskThresholdMin = 0.32f;
        public float maskThresholdRandom = 0.1f;
        public int attemptsMin = 40;
        public int attemptsRandom = 21;
        public float scaleLowBase = 0.5f;
        public float scaleLowRange = 0.3f;
        public float scaleHighBase = 0.8f;
        public float scaleHighRange = 0.4f;
        public float maskCoarseFrequency = 0.06f;
        public float maskFineFrequency = 0.18f;
        public float maskCoarseWeight = 0.65f;
        public float distancePenaltyFactor = 0.5f;
    }
}
