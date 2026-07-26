using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline
{
    // マップ生成アルゴリズムの実装契約。実行時 Config を受け取り生成結果を返す。
    // Contract for a map generation algorithm: takes the runtime config and returns the output.
    public interface IMapGenerator
    {
        MapGenerationOutput Generate(TerrainGenerationConfig config);
    }
}
