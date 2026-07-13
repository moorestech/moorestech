using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "ForestBiome", menuName = "MapGenerator/Biome/Forest")]
    public class ForestBiomeConfig : ScriptableObject
    {
        // 湿度ノイズがこの値以上で森林判定
        [Header("分類")]
        [Label("湿度閾値")]
        [Range(0f, 1f)] public float humidityThreshold = 0.52f;

        // =====================================================================
        // Stage 1: ドメインワープ — ベース形状に有機的なうねりを付与
        // =====================================================================
        [Header("Stage 1: ドメインワープ")]
        [Label("ワープ強度")]
        public float warpStrength = 30f;
        [Label("ワープ反復回数")]
        [Range(1, 4)] public int warpIterations = 2;

        // =====================================================================
        // Stage 2: ベースFBm — 低周波で大きな山塊の骨格を形成
        // =====================================================================
        [Header("Stage 2: ベースFBm（広域うねり）")]
        [Label("ベース周波数")]
        public float baseFrequency = 0.001f;
        [Label("ベースオクターブ数")]
        [Range(2, 6)] public int baseOctaves = 4;
        [Label("ベースパーシステンス")]
        [Range(0.3f, 0.6f)] public float basePersistence = 0.45f;

        // =====================================================================
        // Stage 3: リッジノイズ — FBmの山に乗算でシャープな尾根構造を付与
        // =====================================================================
        [Header("Stage 3: リッジノイズ")]
        [Label("リッジ混合率")]
        [Range(0f, 1f)] public float ridgeBlend = 0f;
        [Label("リッジオクターブ数")]
        [Range(2, 6)] public int ridgeOctaves = 4;

        // =====================================================================
        // Stage 4: 閾値カット＋べき乗 — FBm中間値以下を0に沈め、ピークを急峻に
        // =====================================================================
        [Header("Stage 4: 閾値カット＋べき乗コントラスト")]
        // FBmのこの値以下を0に沈めて山を孤立させる（0.1-0.4が目安）
        [Label("低地カットオフ閾値")]
        [Range(0f, 0.7f)] public float lowlandCutoff = 0.1f;
        [Label("べき乗指数（コントラスト）")]
        [Range(0.3f, 8f)] public float exponent = 1.0f;

        // =====================================================================
        // Stage 5: プラトー平坦化 — smoothstepで山頂にソフトクランプ
        // =====================================================================
        [Header("Stage 5: プラトー平坦化")]
        [Label("プラトー平坦化")]
        [Range(0f, 1f)] public float plateauFlatten = 0f;

        // =====================================================================
        // Stage 6: ディテール加算 — 元座標で均一な凹凸を全域に加算（ワープで歪まない）
        // =====================================================================
        [Header("Stage 6: ディテール加算（表面テクスチャ）")]
        [Label("ディテール周波数")]
        public float detailFrequency = 0.01f;
        [Label("ディテールオクターブ数")]
        [Range(2, 5)] public int detailOctaves = 3;
        [Label("ディテール重み")]
        [Range(0f, 0.5f)] public float detailWeight = 0.04f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + result * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.1f;
        [Label("振幅")]
        public float amplitude = 0.35f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — 森林は木が密生。treeCountを大きくして鬱蒼とした森を表現
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
