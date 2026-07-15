using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "WoodsBiome", menuName = "MapGenerator/Biome/Woods")]
    public class WoodsBiomeConfig : ScriptableObject
    {
        // 草原(~0.40)と森林(0.52~)の間の中湿度帯を林として分類
        [Header("分類")]
        [Label("湿度閾値")]
        [Range(0f, 1f)] public float humidityThreshold = 0.40f;
        [Label("湿度上限")]
        [Range(0f, 1f)] public float humidityUpperThreshold = 0.52f;

        // =====================================================================
        // Stage 1: ベースFBm — 4オクターブfBmで地形の骨格を生成
        // =====================================================================
        [Header("Stage 1: ベースFBm")]
        [Label("周波数")]
        public float frequency = 0.0012f;

        // =====================================================================
        // Stage 2: 段丘化 — fBm出力をTerrace関数で階段状に量子化
        // =====================================================================
        [Header("Stage 2: 段丘化")]
        [Label("段丘段数")]
        [Range(2, 10)] public int terraceSteps = 5;
        [Label("段丘シャープネス")]
        [Range(0f, 1f)] public float terraceSharpness = 0.7f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + terrain * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.05f;
        [Label("振幅")]
        public float amplitude = 0.15f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — 針葉樹混交林。森林より密度は低め
        // =====================================================================
        [Header("Visual 2: 樹木配置")]
        [Label("樹木配置")]
        public TreePlacementConfig treePlacement = new TreePlacementConfig();

        // =====================================================================
        // Visual 3: オブジェクト配置 — 樹木SpatialGrid参照後にPoissonで配置
        // =====================================================================
        [Header("Visual 3: オブジェクト配置")]
        [Label("オブジェクト設定")]
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();

        // =====================================================================
        // Visual 4: 草花ディテール — Tree/ObjectのSpatialGrid参照後に配置
        // =====================================================================
        [Header("Visual 4: 草花ディテール")]
        [Label("ディテール設定")]
        public BiomeDetailConfig detailConfig = new BiomeDetailConfig();

        [Header("海岸設定")]
        [Label("海岸設定")]
        public BiomeShoreConfig shoreConfig = new BiomeShoreConfig();

        [Header("境界設定")]
        [Label("境界設定")]
        public BiomeBoundaryConfig boundaryConfig = new BiomeBoundaryConfig();
    }
}
