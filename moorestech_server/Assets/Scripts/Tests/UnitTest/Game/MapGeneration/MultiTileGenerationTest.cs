using System.Collections.Generic;
using System.Linq;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // gridSizeX/Z の格子ぶんタイルを生成し、原点・タイルindex・配置物のシーン座標が揃うことを検証する。
    // Verifies the generator emits one tile per gridSizeX/Z cell with consistent origins, indices, and scene-space placements.
    public class MultiTileGenerationTest
    {
        private const int GridSide = 3;
        private const int Seed = 7;

        [Test]
        public void グリッド設定どおりのタイル数と原点が出力される()
        {
            var config = BuildConfig(GridSide);

            var output = new VanillaGenerator().Generate(config);

            Assert.AreEqual(GridSide * GridSide, output.Tiles.Count);

            // SceneOrigin = (-half*W, -half*L)。3x3 なら half=1 で中心タイルがシーン (0,W)x(0,L) を占める
            // SceneOrigin = (-half*W, -half*L): with 3x3, half=1, so the center tile occupies scene (0,W)x(0,L)
            Assert.AreEqual(new Vector2(-config.terrainWidth, -config.terrainLength), output.SceneOrigin);

            // 不変条件 NoiseOrigin - SceneOrigin = G。探索無効なら G=0 で master の worldOffset がそのまま残る
            // Invariant NoiseOrigin - SceneOrigin = G; with the search disabled G=0 and the master worldOffset stays
            Assert.AreEqual(new Vector2(config.worldOffsetX, config.worldOffsetZ), output.NoiseOrigin - output.SceneOrigin);

            var indices = output.Tiles.Select(tile => new Vector2Int(tile.TileX, tile.TileZ)).ToList();
            Assert.AreEqual(GridSide * GridSide, indices.Distinct().Count(), "タイルindexが重複している");
            Assert.IsTrue(indices.Contains(new Vector2Int(1, 1)), "中心タイルが無い");

            foreach (var tile in output.Tiles)
            {
                Assert.AreEqual(config.Resolution * config.Resolution, tile.Heights.Length);
                Assert.AreEqual(config.Resolution * config.Resolution, tile.BiomeIndices.Length);
            }
        }

        [Test]
        public void 単一タイル設定では現行と同じ原点になる()
        {
            var config = BuildConfig(1);

            var output = new VanillaGenerator().Generate(config);

            Assert.AreEqual(1, output.Tiles.Count);
            Assert.AreEqual(Vector2.zero, output.SceneOrigin);
        }

        // タイルごとに worldOffset をずらしていなければ全タイルが同じ地形になる。
        // Without shifting worldOffset per tile every tile would carry the very same terrain.
        [Test]
        public void 隣接タイルは別のノイズ窓を見て異なる高さになる()
        {
            var output = new VanillaGenerator().Generate(BuildConfig(GridSide));

            var center = output.Tiles.Single(tile => tile.TileX == 1 && tile.TileZ == 1);
            var right = output.Tiles.Single(tile => tile.TileX == 2 && tile.TileZ == 1);

            Assert.IsFalse(center.Heights.SequenceEqual(right.Heights));
        }

        // 木はタイルローカル座標で配置されるため、タイルのシーン位置ぶん平行移動されないと中心タイルへ折り重なる。
        // Trees are placed in tile-local coordinates and pile onto the center tile unless shifted by the tile's scene position.
        [Test]
        public void 木は全タイルのシーン座標へ広がる()
        {
            var config = BuildConfig(GridSide);
            EnableTrees(config);

            var output = new VanillaGenerator().Generate(config);

            Assert.IsNotEmpty(output.MapObjects);
            var buckets = new HashSet<Vector2Int>();
            foreach (var mapObject in output.MapObjects)
            {
                AssertInsideGrid(mapObject.Position.x, mapObject.Position.z, config);
                buckets.Add(TileBucket(mapObject.Position.x, mapObject.Position.z, config));
            }

            Assert.Less(1, buckets.Count, "木が単一タイルへ固まっている");
            Assert.IsTrue(buckets.Any(bucket => bucket != Vector2Int.zero), "中心タイル以外に木が無い");
        }

        // 鉱脈は output のリストへ加算されるべきで、タイルごとに代入すると最後のタイルぶんしか残らない。
        // Veins must accumulate into the output lists; assigning per tile would keep only the last tile's veins.
        [Test]
        public void 鉱脈は全タイルぶん蓄積される()
        {
            var config = BuildConfig(GridSide);

            var output = new VanillaGenerator().Generate(config);

            Assert.IsNotEmpty(output.ItemVeins);
            var buckets = new HashSet<Vector2Int>();
            foreach (var vein in output.ItemVeins)
            {
                AssertInsideGrid(vein.Min.x, vein.Min.z, config);
                AssertInsideGrid(vein.Max.x, vein.Max.z, config);
                buckets.Add(TileBucket(vein.Min.x, vein.Min.z, config));
            }

            Assert.Less(1, buckets.Count, "鉱脈が単一タイルぶんしか残っていない");
        }

        // 129解像度・探索無効の小さな設定に格子サイズだけ与える（探索有効は本番解像度が要るため使わない）。
        // Takes the small 129-resolution search-disabled setup and only sets the grid size (search requires the production resolution).
        private static TerrainGenerationConfig BuildConfig(int gridSide)
        {
            var config = GenerationRuntimeConfigFactory.Build(TestGenerationConfigFactory.CreateSmall());
            config.seed = Seed;
            config.gridSizeX = gridSide;
            config.gridSizeZ = gridSide;
            return config;
        }

        // テストmodは木を1本も持たないため、既定パラメータの樹種を差し込む。
        // どちらのバイオームが winner になるかは seed 次第なので有効バイオーム両方へ入れる。
        // The test mod ships no trees at all, so inject a default-parameter species.
        // Which biome wins depends on the seed, so both enabled biomes get one.
        private static void EnableTrees(TerrainGenerationConfig config)
        {
            config.grassland.treePlacement = BuildTreePlacement();
            config.forest.treePlacement = BuildTreePlacement();
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

        private static Vector2Int TileBucket(float x, float z, TerrainGenerationConfig config)
        {
            return new Vector2Int(
                Mathf.FloorToInt(x / config.terrainWidth),
                Mathf.FloorToInt(z / config.terrainLength));
        }

        private static void AssertInsideGrid(float x, float z, TerrainGenerationConfig config)
        {
            var minX = -(config.gridSizeX / 2) * config.terrainWidth;
            var minZ = -(config.gridSizeZ / 2) * config.terrainLength;
            Assert.That(x, Is.InRange(minX, minX + config.gridSizeX * config.terrainWidth));
            Assert.That(z, Is.InRange(minZ, minZ + config.gridSizeZ * config.terrainLength));
        }
    }
}
