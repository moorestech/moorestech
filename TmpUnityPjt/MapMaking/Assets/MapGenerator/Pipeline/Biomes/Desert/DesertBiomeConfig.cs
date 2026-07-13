using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "DesertBiome", menuName = "MapGenerator/Biome/Desert")]
    public class DesertBiomeConfig : ScriptableObject
    {
        // 温度ノイズがこの値以下で砂漠判定。BiomeClassifier内で使われる
        [Header("分類")]
        [Label("温度閾値")]
        [Range(0f, 1f)] public float temperatureThreshold = 0.42f;

        // =====================================================================
        // Stage 1: ベース砂丘 (FBm) — 緩やかなfBmで砂漠全体の起伏を生成
        // =====================================================================
        [Header("Stage 1: ベース砂丘 (FBm)")]
        [Label("砂丘ノイズ周波数")]
        public float duneNoiseFrequency = 0.003f;
        [Label("砂丘の振幅")]
        public float duneAmplitude = 0.02f;

        // =====================================================================
        // Stage 2: 渓谷カービング — abs-noise minで台地を谷筋で削る
        // =====================================================================
        [Header("Stage 2: 渓谷カービング")]
        [Label("渓谷の深さ")]
        [Range(0f, 1f)] public float canyonDepth = 0.6f;
        [Label("渓谷周波数")]
        public float canyonFrequency = 0.001f;
        [Label("渓谷オクターブ")]
        [Range(1, 8)] public int canyonOctaves = 4;

        // =====================================================================
        // Stage 3: 崖リッジ — リッジノイズで鋭い尾根を加算
        // =====================================================================
        [Header("Stage 3: 崖リッジ")]
        [Label("崖の高さ")]
        [Range(0f, 0.5f)] public float cliffAmplitude = 0.22f;
        [Label("崖のリッジ周波数")]
        public float cliffFrequency = 0.0012f;
        [Label("崖のオクターブ")]
        [Range(1, 8)] public int cliffOctaves = 4;

        // =====================================================================
        // スムーズ化 — abs()折り返しのゼロ交差を丸める（Stage 2,3で共有）
        // =====================================================================
        [Header("スムーズ化")]
        [Label("折り返しスムーズ化")]
        [Range(0f, 0.3f)] public float absSmoothing = 0.1f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + (base * canyon) + cliff
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.03f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — 砂漠には樹木がないが拡張時のためにフィールドを用意
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
