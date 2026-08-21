using Game.MapGeneration.Pipeline.Config;
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
        // マスタ要素・シード・サーバーデータディレクトリから実行時 Config を組み立てる。
        // サーバーの入口とセッション(クライアント側の再現)が同じ組み立てを通るための切り出し。
        // Builds the runtime config from the master element, seed, and server data directory.
        // Split out so both the server entry point and sessions (client-side reproduction) go through the same assembly.
        public static TerrainGenerationConfig BuildConfig(Generation selected, int seed, string serverDataDirectory)
        {
            var config = GenerationRuntimeConfigFactory.Build(selected);
            config.seed = seed;

            // マスタが持つのは PNG パスだけなので、生成器へ渡す前に画素へ展開しておく。
            // Master only carries PNG paths, so expand them into pixels before handing the config to the generator.
            PlacementNoiseTextureResolver.Resolve(config, serverDataDirectory);
            return config;
        }

        // 組み立て済み Config からアルゴリズムを解決して生成する。
        // Resolves the algorithm from an already-built config and generates.
        public static MapGenerationOutput Generate(Generation selected, TerrainGenerationConfig config)
        {
            var generator = MapGenerationAlgorithmTable.Resolve(selected.Algorithm);
            return generator.Generate(config);
        }

        public static MapGenerationOutput Generate(Generation selected, int seed, string serverDataDirectory)
        {
            var config = BuildConfig(selected, seed, serverDataDirectory);
            return Generate(selected, config);
        }
    }
}
