using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual
{
    // Task 6でFacade/へ移す一時置き場。1タイルぶんの焼成済み地形データ（高さ・alphamap・detail密度）をまとめて運ぶ
    // A temporary home before Task 6 moves it to Facade/; carries one tile's baked terrain data (heights, alphamap, detail densities) together
    public class BakedTerrainTile
    {
        public readonly int TileX;
        public readonly int TileZ;
        public readonly Vector3 TileWorldPosition;

        // 表示用高さ（木の摂動後）。[z, x]
        // Display heights (post-tree perturbation); [z, x]
        public readonly float[,] Heights;

        public readonly float[,,] Alphamap;
        public readonly IReadOnlyList<int[,]> DetailMaps;

        public BakedTerrainTile(
            int tileX, int tileZ, Vector3 tileWorldPosition,
            float[,] heights, float[,,] alphamap, IReadOnlyList<int[,]> detailMaps)
        {
            TileX = tileX;
            TileZ = tileZ;
            TileWorldPosition = tileWorldPosition;
            Heights = heights;
            Alphamap = alphamap;
            DetailMaps = detailMaps;
        }
    }
}
