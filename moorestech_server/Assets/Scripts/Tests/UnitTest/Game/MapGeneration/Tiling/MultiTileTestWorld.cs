using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
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

        // 独立散布の岩に使うGUID。クラスタ採番を通る岩と出自で選り分けるため、別GUIDにしておく。
        // GUID for the independently scattered rocks; a separate one lets tests tell them apart from the cluster-numbered rocks.
        public const string IndependentMapObjectGuid = "00000000-0000-1111-0000-000000000001";

        // テストmodは岩を1本も持たないため、クラスタ採番を通る岩と独立散布の岩を有効バイオーム両方へ差し込む。
        // The test mod ships no rocks, so inject both cluster-numbered and independently scattered rocks into the enabled biomes.
        public static void EnableObjects(TerrainGenerationConfig config)
        {
            config.grassland.objectConfig = BuildObjectConfig();
            config.forest.objectConfig = BuildObjectConfig();
        }

        public static Vector2Int TileBucket(float x, float z, TerrainGenerationConfig config)
        {
            return new Vector2Int(
                Mathf.FloorToInt(x / config.terrainWidth),
                Mathf.FloorToInt(z / config.terrainLength));
        }

        public static void AssertInsideGrid(float x, float z, TerrainGenerationConfig config)
        {
            AssertInsideGridWithMargin(x, z, config, 0f, 0f);
        }

        // 鉱脈は格子外へ1ブロックはみ出すため両隅を余白ぶん緩める
        // A vein overhangs the grid by one block, so both corners take that margin
        public static void AssertVeinInsideGrid(PlacedVein vein, TerrainGenerationConfig config)
        {
            const int margin = TestGenerationConfigFactory.VeinGridOverhang;
            AssertInsideGridWithMargin(vein.Min.x, vein.Min.z, config, margin, margin);
            AssertInsideGridWithMargin(vein.Max.x, vein.Max.z, config, margin, margin);
        }

        // 全ペアをinclusive判定し最初の重なりを報告する
        // Brute-forces every pair inclusively and reports the first overlap
        public static void AssertNoOverlappingVeins(List<PlacedVein> veins)
        {
            for (int i = 0; i < veins.Count; i++)
            for (int j = i + 1; j < veins.Count; j++)
            {
                var a = veins[i];
                var b = veins[j];

                bool overlaps = a.Min.x <= b.Max.x && b.Min.x <= a.Max.x &&
                                a.Min.y <= b.Max.y && b.Min.y <= a.Max.y &&
                                a.Min.z <= b.Max.z && b.Min.z <= a.Max.z;
                Assert.That(overlaps, Is.False,
                    $"鉱脈が重複: A(guid={a.VeinGuid}, Min={a.Min}, Max={a.Max}) vs B(guid={b.VeinGuid}, Min={b.Min}, Max={b.Max})");
            }
        }

        static void AssertInsideGridWithMargin(
            float x, float z, TerrainGenerationConfig config, float marginX, float marginZ)
        {
            var minX = -(config.gridSizeX / 2) * config.terrainWidth;
            var minZ = -(config.gridSizeZ / 2) * config.terrainLength;
            Assert.That(x, Is.InRange(minX - marginX, minX + config.gridSizeX * config.terrainWidth + marginX));
            Assert.That(z, Is.InRange(minZ - marginZ, minZ + config.gridSizeZ * config.terrainLength + marginZ));
        }

        // クラスタ採番を通る岩と、ClusterId=-1 を持つ独立散布の岩を1つずつ。両方が同じタイルに出るのが要点。
        // One cluster-numbered rock plus one independently scattered rock carrying ClusterId=-1; the point is that both land on every tile.
        private static BiomeObjectConfig BuildObjectConfig()
        {
            return new BiomeObjectConfig
            {
                entries = new[]
                {
                    new BiomeObjectConfig.ObjectEntry
                    {
                        mapObjectGuids = new[] { TestGenerationConfigFactory.TestMapObjectGuid },
                        useClusterMode = true,
                        scaleRange = new Vector2(0.5f, 2f),
                    },
                    new BiomeObjectConfig.ObjectEntry
                    {
                        mapObjectGuids = new[] { IndependentMapObjectGuid },
                        useClusterMode = false,
                        scaleRange = new Vector2(0.5f, 2f),
                    },
                },
            };
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
