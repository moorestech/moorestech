using System.Collections.Generic;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     タイル1枚ぶんの見た目データ。高さとバイオームから再構築できるもの＝キャッシュ対象がこの2本
    ///     One tile's visual data; these two arrays are exactly what is rebuildable from heights and biomes, hence cacheable
    /// </summary>
    public class TerrainTileVisual
    {
        // [z, x, layer]。SetAlphamapsがそのまま受け取る並び
        // [z, x, layer], the order SetAlphamaps takes as-is
        public readonly float[,,] Alphamap;

        // detailプロトタイプと同じ並びの密度マップ。各要素は[z, x]
        // Density maps parallel to the detail prototypes; each element is [z, x]
        public readonly IReadOnlyList<int[,]> DetailMaps;

        public TerrainTileVisual(float[,,] alphamap, IReadOnlyList<int[,]> detailMaps)
        {
            Alphamap = alphamap;
            DetailMaps = detailMaps;
        }
    }
}
