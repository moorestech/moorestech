using System.Collections.Generic;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     タイル1枚ぶんの、高さとバイオームから再構築できるデータ＝キャッシュ対象の全て
    ///     Everything rebuildable from heights and biomes for one tile, hence everything the cache holds
    /// </summary>
    public class TerrainTileVisual
    {
        // 木の摂動を足した表示用高さ。[z, x]
        // Display heights with the tree perturbation added; [z, x]
        public readonly float[,] DisplayHeights;

        // 4層/面RGBA8列（z-x-rgba）
        // RGBA8 planes: four layers each, z-x-rgba.
        public readonly IReadOnlyList<byte[]> AlphamapPlanes;

        public readonly int AlphamapResolution;
        public readonly int AlphamapLayerCount;

        // detailプロトタイプと同じ並びの密度マップ。各要素は[z, x]
        // Density maps parallel to the detail prototypes; each element is [z, x]
        public readonly IReadOnlyList<int[,]> DetailMaps;

        public TerrainTileVisual(
            float[,] displayHeights, IReadOnlyList<byte[]> alphamapPlanes, int alphamapResolution, int layerCount,
            IReadOnlyList<int[,]> detailMaps)
        {
            DisplayHeights = displayHeights;
            AlphamapPlanes = alphamapPlanes;
            AlphamapResolution = alphamapResolution;
            AlphamapLayerCount = layerCount;
            DetailMaps = detailMaps;
        }
    }
}
