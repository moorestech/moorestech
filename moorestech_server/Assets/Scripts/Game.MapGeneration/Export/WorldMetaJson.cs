using Newtonsoft.Json;

namespace Game.MapGeneration.Export
{
    // world.jsonのDTO。生成時パラメータの記録用(デバッグ・再現性確認)。書き込みはWorldProvisioner(Task 8)が行う。
    // world.json DTO recording generation parameters for debugging/reproducibility; written by WorldProvisioner (Task 8).
    public class WorldMetaJson
    {
        [JsonProperty("seed")] public int Seed;
        [JsonProperty("generatorVersion")] public string GeneratorVersion;
        [JsonProperty("algorithm")] public string Algorithm;
        [JsonProperty("mapMode")] public string MapMode;
        [JsonProperty("createdAt")] public string CreatedAt;
        [JsonProperty("terrainResolution")] public int TerrainResolution;
        [JsonProperty("terrainTileCount")] public int TerrainTileCount;
    }
}
