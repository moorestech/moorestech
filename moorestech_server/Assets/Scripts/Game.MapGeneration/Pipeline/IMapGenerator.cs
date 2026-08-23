using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline
{
    // マップ生成アルゴリズムの実装契約。実行時 Config を受け取り生成1回ぶんの成果を返す。
    // Contract for a map generation algorithm: takes the runtime config and returns everything the run produced.
    public interface IMapGenerator
    {
        GenerationRun Generate(TerrainGenerationConfig config);
    }
}
