using System.Collections.Generic;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     タイル1枚ぶんの、高さとバイオームから再構築できるデータ＝キャッシュ対象の全て
    ///     alphamapはUnityのalphamapTexturesと同じRGBA8平面で持ち、適用時に変換を挟まずそのまま載せられる形にする
    ///     Everything rebuildable from heights and biomes for one tile, hence everything the cache holds
    ///     The alphamap is kept as the very RGBA8 planes Unity's alphamapTextures use, so applying it needs no conversion
    /// </summary>
    public class TerrainTileVisual
    {
        // 木の摂動を足した表示用高さ。[z, x]
        // Display heights with the tree perturbation added; [z, x]
        public readonly float[,] DisplayHeights;

        // 平面ごとのRGBA8バイト列。1平面が4レイヤーを担い、並びは[z][x][rgba]
        // RGBA8 bytes per plane; one plane carries four layers and the order is [z][x][rgba]
        public readonly IReadOnlyList<byte[]> AlphamapPlanes;

        public readonly int AlphamapResolution;
        public readonly int LayerCount;

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
            LayerCount = layerCount;
            DetailMaps = detailMaps;
        }
    }
}
