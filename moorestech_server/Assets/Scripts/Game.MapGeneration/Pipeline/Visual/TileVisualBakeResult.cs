using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     TileVisualBakerがタイル1枚を焼いた結果。境界の外へはFacadeがBakedTerrainTileへ詰め替えて渡す
    ///     キャッシュ対象のTerrainTileVisualと違い、設置位置という焼いた瞬間にしか無い値も持つ
    ///     What TileVisualBaker produced for one tile; the Facade repacks it into a BakedTerrainTile to cross the boundary
    ///     Unlike the cacheable TerrainTileVisual, it also carries the scene position that exists only at bake time
    /// </summary>
    public class TileVisualBakeResult
    {
        public readonly Vector3 ScenePosition;

        // 表示用高さ（木の摂動後）。[z, x]
        // Display heights (post-tree perturbation); [z, x]
        public readonly float[,] DisplayHeights;

        public readonly TileAlphamap Alphamap;

        public readonly IReadOnlyList<int[,]> DetailMaps;

        public TileVisualBakeResult(
            Vector3 scenePosition, float[,] displayHeights, TileAlphamap alphamap, IReadOnlyList<int[,]> detailMaps)
        {
            ScenePosition = scenePosition;
            DisplayHeights = displayHeights;
            Alphamap = alphamap;
            DetailMaps = detailMaps;
        }
    }
}
