using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "JungleBiome", menuName = "MapGenerator/Biome/Jungle")]
    public class JungleBiomeConfig : ScriptableObject
    {
        // 温度≧tempThreshold かつ 湿度≧humidityThresholdでジャングル判定
        [Header("分類")]
        [Label("温度閾値")]
        [Range(0f, 1f)] public float temperatureThreshold = 0.55f;
        [Label("湿度閾値")]
        [Range(0f, 1f)] public float humidityThreshold = 0.58f;

        // =====================================================================
        // Stage 1: ドメインワープ — Voronoi境界を有機的に歪ませる
        // =====================================================================
        [Header("Stage 1: ドメインワープ")]
        [Label("ドメインワープ強度")]
        [Range(0f, 300f)] public float warpStrength = 30f;
        [Label("ワープオクターブ")]
        [Range(1, 5)] public int warpOctaves = 1;

        // =====================================================================
        // Stage 2: Voronoiセル＋段差割り当て — セルIDから離散段レベルをハッシュ割り当て
        // =====================================================================
        [Header("Stage 2: Voronoiセル＋段差割り当て")]
        [Label("セルサイズ（周波数）")]
        public float terraceFrequency = 0.01f;
        [Label("段差数")]
        [Range(4, 7)] public int terraceStepCount = 7;
        // ブラー後の遷移幅。BoundaryNoiseJobに渡されるガウシアンブラー強度
        [Label("ガウシアンブラー強度")]
        [Range(0f, 1.0f)] public float transitionSmoothing = 0.293f;

        // =====================================================================
        // Stage 3: セル高さバリエーション — 同じ段レベルでもセルごとに高さを変えて単調さを解消
        // =====================================================================
        [Header("Stage 3: セル高さバリエーション")]
        [Label("バリエーション強度")]
        [Range(0f, 1.0f)] public float cellHeightVariation = 0f;

        // =====================================================================
        // Stage 4: 境界スロープ — 1段差の境界にスロープ通路を周期的に配置（2段差以上は常に崖）
        // =====================================================================
        [Header("Stage 4: 境界スロープ")]
        [Label("スロープ幅")]
        [Tooltip("境界からの遷移幅。大きいほどなだらかなランプ。1段差のみ適用")]
        [Range(0f, 1f)] public float slopeWidth = 0.831f;
        [Label("スロープ繰り返し")]
        [Tooltip("境界に沿ったスロープの繰り返し周波数。大きいほど密に配置")]
        [Range(0.5f, 5f)] public float slopeRepeat = 1.3f;
        [Label("スロープ被覆率")]
        [Tooltip("境界全長に対するスロープ部分の割合。0=全崖、1=全スロープ")]
        [Range(0f, 1f)] public float slopeCoverage = 1f;

        // =====================================================================
        // Stage 5: 表面ディテール — 元座標FBmで段上面の平坦さを崩す
        // =====================================================================
        [Header("Stage 5: 表面ディテール")]
        [Label("ディテール周波数")]
        [Tooltip("ノイズの細かさ。大きいほど細かい起伏")]
        [Range(0.01f, 1f)] public float surfaceDetailFrequency = 0.03f;
        [Label("ディテール振幅")]
        [Tooltip("凹凸の深さ。基準面からプラス・マイナス両方向に変位する")]
        [Range(0f, 2f)] public float surfaceDetailAmplitude = 1f;

        // =====================================================================
        // Post: 境界ノイズ — ブラー後の崖面にfBmノイズを加算（別ジョブで実行）
        // =====================================================================
        [Header("Post: 境界ノイズ")]
        [Label("ノイズ強度")]
        [Tooltip("崖面に乗せるノイズの強さ。0=無し")]
        [Range(0f, 40f)] public float boundaryNoiseStrength = 40f;
        [Label("ノイズ周波数")]
        [Tooltip("ノイズの細かさ。大きいほど細かい凹凸")]
        [Range(0.05f, 1f)] public float boundaryNoiseFrequency = 0.509f;
        [Label("勾配閾値")]
        [Tooltip("ノイズが効き始める傾斜角度（度）")]
        [Range(5f, 45f)] public float boundaryNoiseSlopeThreshold = 16.2f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + terrain * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.05f;
        [Label("振幅")]
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
        // Visual 2: 樹木配置 — 熱帯樹木で鬱蒼とした植生（REF密度: 85本/ha）
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
