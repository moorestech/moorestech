using System.Collections.Generic;
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

        // UnityのalphamapTexturesへそのまま載るRGBA8平面。並びは[z][x][rgba]で、1平面が4レイヤーを担う
        // RGBA8 planes going straight onto Unity's alphamapTextures; ordered [z][x][rgba] with one plane per four layers
        public IReadOnlyList<byte[]> AlphamapPlanes { get; }
        public int AlphamapResolution { get; }
        public int AlphamapLayerCount { get; }

        public IReadOnlyList<int[,]> DetailMaps { get; }

        public BakedTerrainTile(
            Vector3 scenePosition, float[,] displayHeights, IReadOnlyList<byte[]> alphamapPlanes,
            int alphamapResolution, int alphamapLayerCount, IReadOnlyList<int[,]> detailMaps)
        {
            ScenePosition = scenePosition;
            DisplayHeights = displayHeights;
            AlphamapPlanes = alphamapPlanes;
            AlphamapResolution = alphamapResolution;
            AlphamapLayerCount = alphamapLayerCount;
            DetailMaps = detailMaps;
        }
    }
}
