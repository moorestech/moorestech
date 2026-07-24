using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Runtime;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline
{
    // 生成パイプラインの薄いエントリポイント。マスタ要素とシードから MapGenerationOutput を返す。
    // アルゴリズムはテーブルでディスパッチし、生成器へ実行時 Config を渡す。
    // Thin entry point of the generation pipeline: master element + seed to MapGenerationOutput.
    // The algorithm is dispatched via the table and the runtime config is handed to the generator.
    public static class MapGenerationPipeline
    {
        public static MapGenerationOutput Generate(Generation selected, int seed)
        {
            // クラスター採番カウンタを毎回リセットし、同一 seed の完全再現性を担保する（決定論保証）。
            // Reset the cluster-id counter each run so same-seed output is fully reproducible (determinism).
            ObjectPlacementMath.NextClusterId = 0;

            var config = GenerationRuntimeConfigFactory.Build(selected);
            config.seed = seed;

            var generator = MapGenerationAlgorithmTable.Resolve(selected.Algorithm);
            return generator(config);
        }
    }
}
