using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Transfer;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline
{
    // 生成パイプラインの薄いエントリポイント。マスタ要素とシードから GenerationRun を返す。
    // アルゴリズムはテーブルでディスパッチし、生成器へ実行時 Config を渡す。
    // Thin entry point of the generation pipeline: master element + seed to a GenerationRun.
    // The algorithm is dispatched via the table and the runtime config is handed to the generator.
    public static class MapGenerationPipeline
    {
        // マスタ・シード・データディレクトリから実行時Configを組立
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

        // 生成時に確定した原点を注入したConfigを組み立てる。ワールド作成時の探索結果を引き継ぐ再現側(クライアント)専用。
        // Builds a config with the origins generation already settled; for the reproducing side (client) that inherits the world-creation search result.
        public static TerrainGenerationConfig BuildConfigWithSettledOrigins(
            Generation selected, int seed, string serverDataDirectory, TerrainOrigins settledOrigins)
        {
            var config = BuildConfig(selected, seed, serverDataDirectory);

            // マスタが探索を使わない設定なら書き戻しが起きず、マスタ値がそのまま生成時の値。注入する余地が無い
            // With the search disabled in the master there is no write-back at all, so the master values are the generation-time ones and nothing needs injecting
            if (!config.useSpawnOffsetSearch) return config;

            // 探索が確定させるのは (worldOffset, spawnWorldPosition) の対だけ。対で復元しないと鉱脈帯とスポーンがズレる
            // The search settles only the (worldOffset, spawnWorldPosition) pair; restoring one without the other skews the vein bands against the spawn
            // worldOffset は中央化オフセット G そのもの(NoiseOrigin = G + SceneOrigin)で、スポーンは探索の成否に依らず G だけずれた spawnTarget に落ちる
            // The worldOffset is the centering offset G itself (NoiseOrigin = G + SceneOrigin), and the spawn lands on spawnTarget shifted by G whether the search succeeded or fell back
            var settledShift = settledOrigins.NoiseOrigin - settledOrigins.SceneOrigin;
            config.spawnWorldPosition = SpawnRegionGeometry.SpawnTargetScenePosition(config) + settledShift;
            config.worldOffsetX = settledShift.x;
            config.worldOffsetZ = settledShift.y;
            config.useSpawnOffsetSearch = false;
            return config;
        }

        // 組み立て済み Config からアルゴリズムを解決して生成する。
        // Resolves the algorithm from an already-built config and generates.
        public static GenerationRun Generate(Generation selected, TerrainGenerationConfig config)
        {
            var generator = MapGenerationAlgorithmTable.Resolve(selected.Algorithm);
            return generator.Generate(config);
        }
    }
}
