using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "GrasslandBiome", menuName = "MapGenerator/Biome/Grassland")]
    public class GrasslandBiomeConfig : ScriptableObject
    {
        // =====================================================================
        // 高さ生成 — Stage 1の大きな起伏にStage 2の小さな凸凹を重ねる
        // =====================================================================
        [Header("Stage 1: ベースPerlin")]
        [Label("周波数")]
        public float frequency = 0.0004f;
        [Label("振幅")]
        public float amplitude = 1f;

        [Header("Stage 2: 小さな凸凹Perlin")]
        [Label("凸凹周波数")]
        public float detailFrequency = 0.02f;
        [Label("凸凹振幅")]
        public float detailAmplitude = 0.08f;

        [Header("Stage 3: 出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.009333f;
        [Label("丘の振幅")]
        [Range(0f, 2f)] public float hillAmplitude = 0.12f;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        // Inspectorからプロトタイプを設定し、SplatmapJobが参照する
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — Poissonサンプリング＋高度/傾斜フィルタで配置
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
