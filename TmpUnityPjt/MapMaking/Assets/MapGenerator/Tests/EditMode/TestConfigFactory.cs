using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// テスト用のTerrainGenerationConfig SOインスタンスを生成するファクトリ。
    /// 全バイオームSOを割り当て済みの状態で返す。
    /// </summary>
    public static class TestConfigFactory
    {
        public static TerrainGenerationConfig Create()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            config.grassland = ScriptableObject.CreateInstance<GrasslandBiomeConfig>();
            config.forest = ScriptableObject.CreateInstance<ForestBiomeConfig>();
            config.savanna = ScriptableObject.CreateInstance<SavannaBiomeConfig>();
            config.desert = ScriptableObject.CreateInstance<DesertBiomeConfig>();
            config.mesa = ScriptableObject.CreateInstance<MesaBiomeConfig>();
            config.alpine = ScriptableObject.CreateInstance<AlpineBiomeConfig>();
            config.jungle = ScriptableObject.CreateInstance<JungleBiomeConfig>();
            config.woods = ScriptableObject.CreateInstance<WoodsBiomeConfig>();
            return config;
        }
    }
}
