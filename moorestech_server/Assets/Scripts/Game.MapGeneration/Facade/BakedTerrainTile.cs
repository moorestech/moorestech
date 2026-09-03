using System.Collections.Generic;
using Game.MapGeneration.Transfer;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    // 1タイルぶんの焼成済み地形データを運ぶ
    // Carries one tile's baked terrain data
    public sealed class BakedTerrainTile
    {
        public Vector3 ScenePosition { get; }

        // 表示用高さ（木の摂動後）。[z, x]
        // Display heights (post-tree perturbation); [z, x]
        public float[,] DisplayHeights { get; }

        public TileAlphamap Alphamap { get; }

        public IReadOnlyList<int[,]> DetailMaps { get; }

        public BakedTerrainTile(
            Vector3 scenePosition, float[,] displayHeights, TileAlphamap alphamap, IReadOnlyList<int[,]> detailMaps)
        {
            ScenePosition = scenePosition;
            DisplayHeights = displayHeights;
            Alphamap = alphamap;
            DetailMaps = detailMaps;
        }
    }
}
