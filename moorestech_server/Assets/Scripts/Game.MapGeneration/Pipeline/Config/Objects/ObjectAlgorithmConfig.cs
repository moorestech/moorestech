namespace Game.MapGeneration.Pipeline.Config
{
    // オブジェクト配置のアルゴリズム定数（ヒーロー岩・従属岩・瓦礫パッチ・クラスタ間隔）。
    // Object placement algorithm constants (hero rock, subordinates, rubble, cluster spacing).
    public class ObjectAlgorithmConfig
    {
        public float heroOffsetFactor = 0.3f;
        public float heroScaleMinRatio = 0.7f;
        public float heroScaleRange = 0.3f;
        public float heroYScaleMin = 0.7f;
        public float heroYScaleRange = 0.15f;
        public float subordinateDistMin = 0.35f;
        public float subordinateDistRange = 0.65f;
        public float subordinateAngleReject = 55f;
        public float subordinateScaleMaxRatio = 0.6f;
        public float subordinateYScaleMin = 0.5f;
        public float subordinateYScaleRange = 0.3f;
        public float saddleProbability = 0.65f;
        public float saddleJitter = 3f;
        public float biasSectorAngle = 0.67f;
        public float rubbleSizeMin = 0.5f;
        public float rubbleSizeRange = 1.0f;
        public float rubbleDensityMultiplier = 5f;
        public float clusterSpacingFactor = 0.6f;
    }
}
