namespace Game.MapGeneration.Pipeline.Config
{
    // バイオームごとの樹木配置設定。各プロトタイプが独立した配置パイプラインを持つ。
    // Per-biome tree placement config; each prototype has an independent placement pipeline.
    public class TreePlacementConfig
    {
        public TreePrototypeEntry[] prototypes;
    }
}
