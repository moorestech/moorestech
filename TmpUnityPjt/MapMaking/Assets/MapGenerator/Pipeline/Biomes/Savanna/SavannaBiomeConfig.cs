using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "SavannaBiome", menuName = "MapGenerator/Biome/Savanna")]
    public class SavannaBiomeConfig : ScriptableObject
    {
        // 温度ノイズがこの値以上でサバンナ判定（暖かい地域）
        [Header("分類")]
        [Label("温度閾値")]
        [Range(0f, 1f)] public float temperatureThreshold = 0.55f;

        // =====================================================================
        // Stage 1: ドメインワープ — frequency*2.5でワープし台地輪郭を歪ませる
        // =====================================================================
        [Header("Stage 1: ドメインワープ")]
        // warp/detailの両方で使われる基準周波数
        [Label("周波数")]
        public float frequency = 0.0015f;

        // =====================================================================
        // Stage 2: 台地位置ノイズ — ワープ後座標でFBmし台地の場所を決定
        // =====================================================================
        [Header("Stage 2: 台地位置ノイズ")]
        [Label("台地周波数")]
        [Tooltip("台地ノイズの周波数。低いほど大きな台地、高いほど小さく密な台地")]
        public float plateauFrequency = 0.0015f;

        // =====================================================================
        // Stage 3: 閾値正規化 — この値以下は平原、以上を0-1正規化してテラス化
        // =====================================================================
        [Header("Stage 3: 閾値正規化")]
        [Label("台地の出現閾値")]
        [Range(0.3f, 0.8f)] public float hillThreshold = 0.552f;

        // =====================================================================
        // Stage 4: テラス量子化 — 正規化値を離散段に量子化しsmoothstep遷移
        // =====================================================================
        [Header("Stage 4: テラス量子化")]
        [Label("テラス段数")]
        [Tooltip("台地の段数。段数が多いほど段々畑のような形状になる")]
        [Range(1, 5)] public int plateauSharpness = 4;

        // =====================================================================
        // Stage 5: 平原起伏 — ワープFBmを再利用した丘陵起伏（台地が高いほど減衰）
        // =====================================================================
        [Header("Stage 5: 平原起伏")]
        [Label("起伏振幅")]
        [Tooltip("平原の丘陵起伏の強さ。0で完全に平坦")]
        [Range(0f, 0.15f)] public float undulationAmplitude = 0.02f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + undulation + detail + shaped * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.03f;
        [Label("台地の高さ")]
        public float amplitude = 0.2f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — アカシアやバオバブなどの疎らな配置
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
