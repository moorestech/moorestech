using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Pipeline.Stages;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling.Seam;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 中央化オフセット G をノイズ座標にだけ効かせ、出力を格子のシーン座標へ戻すことを検証する。
    // Verifies G affects only noise coordinates and all outputs return to scene space inside the generated grid.
    public class SpawnOffsetSceneSpaceTest
    {
        private const int Seed = 12345;

        [Test]
        public void SpawnAndPlacementsAreInsideGridWhenSpawnSearchSucceeds()
        {
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled);

            // 探索が実際に成功し G が非ゼロであることを先に確定させる（G=0 だと -G の検証にならない）。
            // Establish that the search really succeeded with a non-zero G, otherwise -G would be untested.
            var searchResult = FindSpawnRegion(generation);
            Assert.That(searchResult.Success, Is.True, searchResult.Diagnostics);
            Assert.That(searchResult.WorldOffset.magnitude, Is.GreaterThan(1f));

            // 生成ログに探索の成否と診断が残ることを固定する（ADR#13: フォールバックを無言にしない）
            // Pin that generation logs the search outcome and diagnostics (ADR#13: fallbacks are never silent)
            LogAssert.Expect(LogType.Log, new Regex(@"\[SpawnSearch\] 成功\n.+"));

            var output = SpawnSearchTestWorld.AssertOutputIsInsideGrid(generation, Seed);

            // 探索が選んだ良地が実際に生成されていれば、スポーン地点の分類は Grassland になる。
            // If the region the search chose was really generated, the spawn point classifies as Grassland.
            var config = MapGenerationPipeline.BuildConfig(generation, Seed, TestGenerationConfigFactory.ServerDataDirectory);
            Assert.That(BiomeAtSpawn(output, generation, config), Is.EqualTo(BiomeType.Grassland));

            // world.json へ永続化されクライアントの分類段が使う値。index(0,0)タイルの窓原点は G + SceneOrigin。
            // These are persisted to world.json and drive the client's classification stage: the index(0,0) tile's window origin is G + SceneOrigin.
            var expectedSceneOrigin = SpawnSearchTestWorld.ExpectedSceneOrigin(generation);
            Assert.That(output.SceneOrigin, Is.EqualTo(expectedSceneOrigin));
            Assert.That(output.NoiseOrigin, Is.EqualTo(searchResult.WorldOffset + expectedSceneOrigin));
        }

        [Test]
        public void SpawnAndPlacementsAreInsideGridWhenSpawnSearchFallsBack()
        {
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Unsatisfiable);

            var searchResult = FindSpawnRegion(generation);
            Assert.That(searchResult.Success, Is.False);

            // フォールバックでも診断がログに残ることを固定する（ADR#13: 無言で海にスポーンさせない）
            // Pin that the fallback outcome is logged too (ADR#13: never strand the spawn silently)
            LogAssert.Expect(LogType.Log, new Regex(@"\[SpawnSearch\] フォールバック\n.+"));

            // シーン原点は G に依らず格子形状だけで決まる。絶対値で固定しないとスポーンと原点が同量ずれても通る
            // The scene origin depends only on the grid shape, not on G; without pinning it absolutely, spawn and origin could drift together unnoticed
            var output = SpawnSearchTestWorld.AssertOutputIsInsideGrid(generation, Seed);
            Assert.That(output.SceneOrigin, Is.EqualTo(SpawnSearchTestWorld.ExpectedSceneOrigin(generation)));
        }

        // 探索は master の worldOffsetX を見ずに絶対ノイズ空間で S を決めるため、G は上書きであって加算ではない。
        // 加算にすると生成地形が master の基底ぶんズレ、探索が検証した地形と別物になる。
        // The search picks S in absolute noise space without reading the master worldOffsetX, so G replaces it rather than adding.
        // Adding would shift the generated terrain by the master base, making it a different place than the search verified.
        [Test]
        public void MasterWorldOffsetDoesNotMoveTerrainWhenSpawnSearchSucceeds()
        {
            var atOrigin = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled);
            var shifted = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled,
                new JObject { ["worldOffsetX"] = 317.0, ["worldOffsetZ"] = -213.0 });

            var expectedConfig = MapGenerationPipeline.BuildConfig(atOrigin, Seed, TestGenerationConfigFactory.ServerDataDirectory);
            var expected = MapGenerationPipeline.Generate(atOrigin, expectedConfig).Output;
            var actualConfig = MapGenerationPipeline.BuildConfig(shifted, Seed, TestGenerationConfigFactory.ServerDataDirectory);
            var actual = MapGenerationPipeline.Generate(shifted, actualConfig).Output;

            Assert.That(actual.SpawnPoint, Is.EqualTo(expected.SpawnPoint));
            Assert.That(actual.Tiles.Count, Is.EqualTo(expected.Tiles.Count));

            // 隅タイルだけ見ると格子中央のズレを見逃すため、全タイルを index で突き合わせる
            // Checking only the corner tile would miss a shift at the grid's middle, so pair every tile up by index
            for (int i = 0; i < expected.Tiles.Count; i++)
            {
                var expectedTile = expected.Tiles[i];
                var actualTile = actual.Tiles[i];
                Assert.That(actualTile.TileX, Is.EqualTo(expectedTile.TileX));
                Assert.That(actualTile.TileZ, Is.EqualTo(expectedTile.TileZ));

                // 長さ違いを別assertに分ける。index=0 を流用すると「index=0で値が違う」と誤読されるため
                // Length mismatch gets its own assert; reusing index=0 for it would misread as "index 0's value differs"
                Assert.That(actualTile.Heights.Length, Is.EqualTo(expectedTile.Heights.Length));

                int differentIndex = FirstDifferentIndex(expectedTile.Heights, actualTile.Heights);
                Assert.That(differentIndex, Is.EqualTo(-1),
                    $"master の worldOffset がハイトマップを動かした: tile=({expectedTile.TileX},{expectedTile.TileZ}) index={differentIndex}");
            }
        }

        [Test]
        public void スポーン安全域内のMapObjectだけを除外する()
        {
            // 近距離配置だけを除き、境界外の配置順序と座標を維持する
            // Remove only the near placement while preserving the order and position outside the boundary
            // 除外はXZ位置だけで決まるので、種別と見た目の効き方は結果に関与しない
            // The exclusion is decided by the XZ position alone, so the kind and the surround effect play no part
            var entries = new List<PlacementEntry>
            {
                PlacementEntry.CreateTree(string.Empty, new Vector3(3f, 0f, 4f),
                    Quaternion.identity, Vector3.one, 0f, TerrainSurroundEffectType.treeRootPatch),
                PlacementEntry.CreateTree(string.Empty, new Vector3(20f, 0f, 0f),
                    Quaternion.identity, Vector3.one, 0f, TerrainSurroundEffectType.treeRootPatch),
            };
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(entries, Vector3.zero);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].WorldPosition, Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }

        // 長さが等しいことは呼び出し元が既にassert済みの前提で走査する
        // Assumes the caller already asserted equal lengths before scanning
        private static int FirstDifferentIndex(float[] expected, float[] actual)
        {
            for (int i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i]) return i;
            return -1;
        }

        private static SpawnSearchResult FindSpawnRegion(Generation generation)
        {
            var config = GenerationRuntimeConfigFactory.Build(generation);
            config.seed = Seed;
            return SpawnRegionFinder.Find(config, ClassificationStage.GetEnabledBiomeTypes(config));
        }

        // スポーン地点のシーン座標を中心タイルのハイトマップ格子へ写し、その画素のバイオームを返す。
        // Maps the spawn scene position onto the center tile's heightmap lattice and returns that pixel's biome.
        private static BiomeType BiomeAtSpawn(MapGenerationOutput output, Generation generation, TerrainGenerationConfig config)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            int res = output.Resolution;

            // 中心タイルはシーン (0,W)x(0,L) を占めるので、格子の原点は SceneOrigin ではなく 0 である。
            // The center tile occupies scene (0,W)x(0,L), so the lattice origin is 0, not SceneOrigin.
            int px = Mathf.RoundToInt(output.SpawnPoint.x / vp.TerrainWidth * (res - 1));
            int pz = Mathf.RoundToInt(output.SpawnPoint.z / vp.TerrainLength * (res - 1));

            // 中心タイルは index (half, half)。Tiles[0] は隅タイルなので添字が範囲外になる
            // The center tile is index (half, half); Tiles[0] is a corner tile and the index would run past its end
            int half = SpawnSearchTestWorld.GridSide / 2;
            var biomeIndices = TileBiomeIndexComputer.ComputeForTile(config, output, half, half);
            return (BiomeType)biomeIndices[pz * res + px];
        }
    }
}
