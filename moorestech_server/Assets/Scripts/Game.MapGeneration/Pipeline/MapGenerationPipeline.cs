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
        public static MapGenerationOutput Generate(Generation selected, int seed, string serverDataDirectory)
        {
            var config = GenerationRuntimeConfigFactory.Build(selected);
            config.seed = seed;

            // マスタが持つのは PNG パスだけなので、生成器へ渡す前に画素へ展開しておく。
            // Master only carries PNG paths, so expand them into pixels before handing the config to the generator.
            PlacementNoiseTextureResolver.Resolve(config, serverDataDirectory);

            var generator = MapGenerationAlgorithmTable.Resolve(selected.Algorithm);
            return generator.Generate(config);
        }
    }
}
