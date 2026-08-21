using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    // 1タイルぶんの焼成済み地形データ(高さ・alphamap・detail密度)をまとめて運ぶ
    // Carries one tile's baked terrain data (heights, alphamap, detail densities) together
    public sealed class BakedTerrainTile
    {
        public int TileX { get; }
        public int TileZ { get; }
        public Vector3 ScenePosition { get; }

        // 表示用高さ（木の摂動後）。[z, x]
        // Display heights (post-tree perturbation); [z, x]
        public float[,] DisplayHeights { get; }

        public float[,,] Alphamap { get; }
        public IReadOnlyList<int[,]> DetailMaps { get; }

        public BakedTerrainTile(
            int tileX, int tileZ, Vector3 scenePosition,
            float[,] displayHeights, float[,,] alphamap, IReadOnlyList<int[,]> detailMaps)
        {
            TileX = tileX;
            TileZ = tileZ;
            ScenePosition = scenePosition;
            DisplayHeights = displayHeights;
            Alphamap = alphamap;
            DetailMaps = detailMaps;
        }
    }
}
