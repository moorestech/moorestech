using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     1バイオーム分のDetail評価で全ピクセルが共有する読み取り専用の入力一式
    ///     The read-only inputs every pixel shares while evaluating details for one biome
    /// </summary>
    public readonly struct DetailSampleContext
    {
        public readonly bool[,] Mask;
        public readonly float[,] Slopes;

        // 曲率・方位角はそれを使うフィルタが1つも無ければ計算されずnullのまま渡る
        // Curvature and azimuth stay null when no filter uses them, so they are never computed needlessly
        public readonly float[,] Curvature;
        public readonly float[,] Azimuth;

        // Tree/Objectまでの距離場。供給責任は呼び出し側にあり、無ければ該当フィルタは効かない
        // Distance fields to trees and objects; the caller supplies them and their filters idle when absent
        public readonly float[,] TreeDistanceMap;
        public readonly float[,] ObjectDistanceMap;

        public readonly float[,,] Splatmap;
        public readonly TerrainLayer[] TerrainLayers;
        public readonly Vector2[] NoiseOffsets;

        public readonly int HeightmapResolution;
        public readonly int DetailResolution;
        public readonly int SplatResolution;

        public readonly float TerrainWidth;
        public readonly float TerrainLength;

        // タイル原点のワールド座標。捨てると全タイルが同じ草の粗密を反復し、境目で模様が直線状に切り替わる
        // The tile origin in world space; dropping it repeats one grass pattern on every tile and cuts a straight seam at the boundary
        public readonly float WorldOffsetX;
        public readonly float WorldOffsetZ;

        public readonly float FilterRejectThreshold;
        public readonly float BorderMarginPixels;

        public DetailSampleContext(
            bool[,] mask, float[,] slopes, float[,] curvature, float[,] azimuth,
            float[,] treeDistanceMap, float[,] objectDistanceMap,
            float[,,] splatmap, TerrainLayer[] terrainLayers, Vector2[] noiseOffsets,
            int heightmapResolution, int detailResolution, int splatResolution,
            float terrainWidth, float terrainLength, float worldOffsetX, float worldOffsetZ,
            float filterRejectThreshold, float borderMarginPixels)
        {
            Mask = mask;
            Slopes = slopes;
            Curvature = curvature;
            Azimuth = azimuth;
            TreeDistanceMap = treeDistanceMap;
            ObjectDistanceMap = objectDistanceMap;
            Splatmap = splatmap;
            TerrainLayers = terrainLayers;
            NoiseOffsets = noiseOffsets;
            HeightmapResolution = heightmapResolution;
            DetailResolution = detailResolution;
            SplatResolution = splatResolution;
            TerrainWidth = terrainWidth;
            TerrainLength = terrainLength;
            WorldOffsetX = worldOffsetX;
            WorldOffsetZ = worldOffsetZ;
            FilterRejectThreshold = filterRejectThreshold;
            BorderMarginPixels = borderMarginPixels;
        }
    }
}
