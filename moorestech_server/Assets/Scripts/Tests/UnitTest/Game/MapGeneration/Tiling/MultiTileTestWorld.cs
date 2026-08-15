using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 多タイル生成テストが共有する「小さな格子ワールド」の組み立てと、その格子に対する位置判定をまとめる。
    // Builds the small grid world shared by the multi-tile generation tests and locates points against that grid.
    public static class MultiTileTestWorld
    {
        // 129解像度・探索無効の小さな設定に格子サイズだけ与える（探索有効は本番解像度が要るため使わない）。
        // Takes the small 129-resolution search-disabled setup and only sets the grid size (search requires the production resolution).
        public static TerrainGenerationConfig BuildConfig(int gridSide, int seed)
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = seed;
            config.gridSizeX = gridSide;
            config.gridSizeZ = gridSide;
            return config;
        }

        // テストmodは木を1本も持たないため、既定パラメータの樹種を有効バイオーム両方へ差し込む（winnerはseed次第）。
        // The test mod ships no trees, so inject a default-parameter species into both enabled biomes (the winner depends on the seed).
        public static void EnableTrees(TerrainGenerationConfig config)
        {
            config.grassland.treePlacement = BuildTreePlacement();
            config.forest.treePlacement = BuildTreePlacement();
        }

        public static Vector2Int TileBucket(float x, float z, TerrainGenerationConfig config)
        {
            return new Vector2Int(
                Mathf.FloorToInt(x / config.terrainWidth),
                Mathf.FloorToInt(z / config.terrainLength));
        }

        public static void AssertInsideGrid(float x, float z, TerrainGenerationConfig config)
        {
            var minX = -(config.gridSizeX / 2) * config.terrainWidth;
            var minZ = -(config.gridSizeZ / 2) * config.terrainLength;
            Assert.That(x, Is.InRange(minX, minX + config.gridSizeX * config.terrainWidth));
            Assert.That(z, Is.InRange(minZ, minZ + config.gridSizeZ * config.terrainLength));
        }

        private static TreePlacementConfig BuildTreePlacement()
        {
            return new TreePlacementConfig
            {
                prototypes = new[]
                {
                    new TreePrototypeEntry
                    {
                        mapObjectGuids = new[] { TestGenerationConfigFactory.TestMapObjectGuid },
                    },
                },
            };
        }
    }
}
