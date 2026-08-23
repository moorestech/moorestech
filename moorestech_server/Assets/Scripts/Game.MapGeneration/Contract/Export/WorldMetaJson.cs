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

        // 生成時に実際に使ったノイズ窓の原点。スポーン探索の中央化オフセットGを含むためマスタからは復元できない
        // The noise window origin actually used at generation; it embeds the spawn-search centering offset G and cannot be recovered from the master
        // nullableなのはキー欠損を0と区別するため。generatedで欠損なら例外にする判定はTerrainTransferMetaReaderが持つ
        // Nullable so a missing key is distinguishable from 0; TerrainTransferMetaReader owns the generated-only requirement
        [JsonProperty("terrainNoiseOriginX")] public float? TerrainNoiseOriginX;
        [JsonProperty("terrainNoiseOriginZ")] public float? TerrainNoiseOriginZ;

        // 生成タイルがシーン上で占める原点。map.jsonの座標もこの原点基準で、地形もここへ置かれる
        // Scene-space origin of the generated tile; map.json coordinates share this origin and the terrain is placed there
        [JsonProperty("terrainSceneOriginX")] public float? TerrainSceneOriginX;
        [JsonProperty("terrainSceneOriginZ")] public float? TerrainSceneOriginZ;

        // 生成マスタの指紋(JSON原文+配置ノイズPNG)。generatedのみ書く。templateはnullで「概念自体が無い」を表明する
        // The generation master's fingerprint (JSON text + placement-noise PNGs), written only for generated; null for template declares the concept itself is absent
        [JsonProperty("generationMasterFingerprint")] public string GenerationMasterFingerprint;
    }
}
