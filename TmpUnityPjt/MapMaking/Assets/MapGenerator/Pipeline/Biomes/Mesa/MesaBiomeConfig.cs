using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "MesaBiome", menuName = "MapGenerator/Biome/Mesa")]
    public class MesaBiomeConfig : ScriptableObject
    {
        // 標高ノイズ≧elevationThreshold かつ 湿度ノイズ≦humidityThresholdでメサ判定
        [Header("分類")]
        [Label("標高閾値")]
        [Range(0f, 1f)] public float elevationThreshold = 0.42f;
        [Label("湿度閾値（以下）")]
        [Range(0f, 1f)] public float humidityThreshold = 0.38f;

        // =====================================================================
        // Stage 1: ドメインワープ — ビュート輪郭を有機的に歪める
        // =====================================================================
        [Header("Stage 1: ドメインワープ")]
        [Label("ワープ強度")]
        public float warpStrength = 140f;
        [Label("ワープ反復")]
        [Range(0, 3)] public int warpIterations = 1;

        // =====================================================================
        // Stage 2: 積ノイズ — 2つの独立fBm場の積でビュートを自然に孤立化
        // =====================================================================
        [Header("Stage 2: 積ノイズ（ビュート形成）")]
        [Label("周波数")]
        public float frequency = 0.002f;
        [Label("オクターブ数")]
        [Range(1, 8)] public int octaves = 1;
        [Label("持続性")]
        [Range(0.1f, 0.9f)] public float persistence = 0.5f;
        // 第2fBm場の周波数倍率。1.0で同一スケール、高いほど小さなビュート
        [Label("孤立化ノイズ周波数倍率")]
        [Tooltip("第2fBm場の周波数倍率。1.0で同一スケール、高いほど小さなビュート")]
        [Range(1f, 3f)] public float isolationFreqMult = 2f;

        // =====================================================================
        // Stage 3: 渓谷カービング — abs-noise minでビュート間に谷を刻む
        // =====================================================================
        [Header("Stage 3: 渓谷カービング")]
        [Label("渓谷深さ")]
        [Range(0f, 1f)] public float canyonDepth = 0.297f;
        [Label("渓谷周波数倍率")]
        public float canyonFreqMult = 0.1f;
        [Label("渓谷オクターブ")]
        [Range(1, 8)] public int canyonOctaves = 3;

        // =====================================================================
        // Stage 4: 境界ノイズ — 閾値前にノイズを加算して台地の輪郭をギザギザに
        // =====================================================================
        [Header("Stage 4: 境界ノイズ")]
        [Label("境界ノイズ強度")]
        [Tooltip("台地の輪郭の複雑さ。大きいほどギザギザ")]
        [Range(0f, 0.3f)] public float boundaryNoiseStrength = 0.3f;
        [Label("境界ノイズ周波数倍率")]
        [Range(1f, 10f)] public float boundaryNoiseFreqMult = 4f;
        [Label("境界ノイズオクターブ")]
        [Range(1, 5)] public int boundaryNoiseOctaves = 4;

        // =====================================================================
        // Stage 5: ビュートプロファイル — smoothstep閾値で立ち上がりを形成
        // =====================================================================
        [Header("Stage 5: ビュートプロファイル")]
        [Label("ビュート閾値")]
        [Tooltip("積ノイズ（0〜1の積≈0〜0.5付近に集中）に対する閾値")]
        [Range(0.05f, 0.5f)] public float butteThreshold = 0.321f;
        [Label("崖の急峻さ")]
        [Tooltip("大きいほど急峻。10以上はハイトマップのジャギが出るので注意")]
        [Range(3f, 20f)] public float cliffSteepness = 7f;

        // =====================================================================
        // Stage 6: プラトー平坦化＋崖面テラス — 頂部を平坦化し崖に地層段差を刻む
        // =====================================================================
        [Header("Stage 6: プラトー平坦化＋崖面テラス")]
        [Label("プラトー平坦化")]
        [Tooltip("0=なし、0.3=頂上と谷底をフラットに。高いほど台地感が増す")]
        [Range(0f, 0.5f)] public float plateauFlatten = 0f;
        // 崖面に地層の段差を刻み、メサ特有の水平バンドを表現
        [Label("テラス段数")]
        [Tooltip("崖面の水平地層バンド数。0-1で無効")]
        [Range(0, 8)] public int terraceSteps = 3;
        [Label("テラスシャープネス")]
        [Tooltip("段差の明瞭さ。高いほどくっきりした地層")]
        [Range(0f, 1f)] public float terraceSharpness = 0.99f;

        // =====================================================================
        // Stage 7: 谷底ノイズ — 台地下の砂漠微地形（中周波うねり+高周波ザラつき）
        // =====================================================================
        [Header("Stage 7: 谷底ノイズ")]
        [Label("谷底ノイズ強度")]
        [Tooltip("谷底の砂漠微地形。中周波うねり+高周波ザラつき")]
        [Range(0f, 0.15f)] public float floorVariation = 0.102f;

        // =====================================================================
        // Stage 8: 台地上ノイズ — 風化した岩盤の微地形（台地上のみ）
        // =====================================================================
        [Header("Stage 8: 台地上ノイズ")]
        [Label("台地上ノイズ強度")]
        [Tooltip("台地頂上の風化した岩盤の微地形")]
        [Range(0f, 0.15f)] public float topNoiseStrength = 0.051f;
        [Label("台地上ノイズ周波数倍率")]
        [Tooltip("台地上の凹凸スケール。大きいほど細かい")]
        [Range(2f, 15f)] public float topNoiseFreqMult = 5f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + result * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.05f;
        [Label("振幅")]
        public float amplitude = 0.1f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — サボテンなど最小限の植生
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
