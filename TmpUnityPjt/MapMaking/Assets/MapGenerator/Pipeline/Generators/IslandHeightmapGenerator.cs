namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// ハイトマップ生成結果の互換構造体。
    /// DOTSジョブパイプラインの出力をステージ3-5の配置処理に渡すための中間形式。
    /// 旧5フェーズオーケストレータは削除済み（ClassificationJob等のDOTSジョブに移行）。
    /// </summary>
    public static class IslandHeightmapGenerator
    {
        public struct GenerationResult
        {
            public float[] heights;
            public float[] biomeMap;
            // [pixelIndex, layerIndex]: Ocean(0), Beach(1), コンテンツバイオーム(2+)
            public float[,] biomeWeights;
        }
    }
}
