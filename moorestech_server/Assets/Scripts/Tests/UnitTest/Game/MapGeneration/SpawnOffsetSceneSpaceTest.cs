using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Pipeline.Stages;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 中央化オフセット G をノイズ座標にだけ効かせ、出力を単一タイルのシーン座標へ戻すことを検証する。
    // Verifies G affects only noise coordinates and all outputs return to scene space inside the single generated tile.
    public class SpawnOffsetSceneSpaceTest
    {
        private const int Seed = 12345;

        [Test]
        public void SpawnAndPlacementsAreInsideTileWhenSpawnSearchSucceeds()
        {
            var generation = TestGenerationConfigFactory.Create(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled);

            // 探索が実際に成功し G が非ゼロであることを先に確定させる（G=0 だと -G の検証にならない）。
            // Establish that the search really succeeded with a non-zero G, otherwise -G would be untested.
            var searchResult = FindSpawnRegion(generation);
            Assert.That(searchResult.Success, Is.True, searchResult.Diagnostics);
            Assert.That(searchResult.WorldOffset.magnitude, Is.GreaterThan(1f));

            var output = AssertOutputIsInsideTile(generation);

            // 探索が選んだ良地が実際に生成されていれば、スポーン地点の分類は Grassland になる。
            // If the region the search chose was really generated, the spawn point classifies as Grassland.
            Assert.That(BiomeAtSpawn(output, generation), Is.EqualTo(BiomeType.Grassland));

            // world.json へ永続化されクライアントの分類段が使う値。ノイズ窓は G の位置、シーン原点はタイル基準の 0。
            // These are persisted to world.json and drive the client's classification stage: the noise window sits at G, the scene origin at the tile's 0.
            Assert.That(output.NoiseOrigin, Is.EqualTo(searchResult.WorldOffset));
            Assert.That(output.SceneOrigin, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SpawnAndPlacementsAreInsideTileWhenSpawnSearchFallsBack()
        {
            var generation = TestGenerationConfigFactory.Create(
                TestGenerationConfigFactory.SpawnSearchSetup.Unsatisfiable);

            var searchResult = FindSpawnRegion(generation);
            Assert.That(searchResult.Success, Is.False);

            // フォールバックは G=0 なのでシーン原点もタイルの0。絶対値で固定しないとスポーンと原点が同量ずれても通る
            // The fallback has G=0, so the scene origin is the tile's 0; without pinning it absolutely, spawn and origin could drift together unnoticed
            var output = AssertOutputIsInsideTile(generation);
            Assert.That(output.SceneOrigin, Is.EqualTo(Vector2.zero));
        }

        // 格子中心はgridSizeが偶数だとタイル角(0,0)に落ち、スポーンが地形の角に張り付く。無言で通さず生成を落とす
        // An even gridSize drops the grid center onto the tile corner (0,0), pinning the spawn to the terrain corner; generation must throw rather than pass silently
        [Test]
        public void 偶数gridSizeで格子中心がタイル外へ落ちるならワールド生成を落とす()
        {
            var generation = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled,
                new JObject { ["gridSizeX"] = 4, ["gridSizeZ"] = 4 });

            Assert.Throws<InvalidOperationException>(() => MapGenerationPipeline.Generate(generation, Seed));
        }

        // 探索は master の worldOffsetX を見ずに絶対ノイズ空間で S を決めるため、G は上書きであって加算ではない。
        // 加算にすると生成地形が master の基底ぶんズレ、探索が検証した地形と別物になる。
        // The search picks S in absolute noise space without reading the master worldOffsetX, so G replaces it rather than adding.
        // Adding would shift the generated terrain by the master base, making it a different place than the search verified.
        [Test]
        public void MasterWorldOffsetDoesNotMoveTerrainWhenSpawnSearchSucceeds()
        {
            var atOrigin = TestGenerationConfigFactory.Create(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled);
            var shifted = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled,
                new JObject { ["worldOffsetX"] = 317.0, ["worldOffsetZ"] = -213.0 });

            var expected = MapGenerationPipeline.Generate(atOrigin, Seed);
            var actual = MapGenerationPipeline.Generate(shifted, Seed);

            Assert.That(actual.SpawnPoint, Is.EqualTo(expected.SpawnPoint));
            Assert.That(actual.Heights.Length, Is.EqualTo(expected.Heights.Length));

            int differentIndex = FirstDifferentIndex(expected.Heights, actual.Heights);
            Assert.That(differentIndex, Is.EqualTo(-1),
                $"master の worldOffset がハイトマップを動かした: index={differentIndex}");
        }

        // 探索無効時のスポーンは master 値そのままで生成タイルの外を指しうる。clamp で吸収すると地形外スポーンが残る。
        // With the search off the spawn stays at the master value and can point outside the generated tile; clamping it would leave an off-terrain spawn.
        [Test]
        public void SpawnOutsideTheGeneratedTileThrowsWhenSpawnSearchDisabled()
        {
            var generation = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled,
                new JObject { ["spawnWorldPosition"] = new JArray(2116.69922, -807.6172) });

            Assert.Throws<InvalidOperationException>(() => MapGenerationPipeline.Generate(generation, Seed));
        }

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

        // スポーン地点のシーン座標をハイトマップ格子へ写し、その画素のバイオームを返す。
        // 格子の原点は master の worldOffset ではなく、生成が確定させた SceneOrigin である。
        // Maps the spawn scene position onto the heightmap lattice and returns that pixel's biome.
        // The lattice origin is the SceneOrigin generation settled on, not the master worldOffset.
        private static BiomeType BiomeAtSpawn(MapGenerationOutput output, Generation generation)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            int res = output.Resolution;
            int px = Mathf.RoundToInt((output.SpawnPoint.x - output.SceneOrigin.x) / vp.TerrainWidth * (res - 1));
            int pz = Mathf.RoundToInt((output.SpawnPoint.z - output.SceneOrigin.y) / vp.TerrainLength * (res - 1));
            return (BiomeType)output.BiomeIndices[pz * res + px];
        }

        private static MapGenerationOutput AssertOutputIsInsideTile(Generation generation)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            var output = MapGenerationPipeline.Generate(generation, Seed);

            // タイルが占める範囲を決めるのは SceneOrigin。master の worldOffset を原点に使うとタイルの実位置とずれる。
            // SceneOrigin decides the tile's extent; using the master worldOffset as origin would diverge from where the tile really is.
            float minX = output.SceneOrigin.x;
            float minZ = output.SceneOrigin.y;
            float maxX = minX + vp.TerrainWidth;
            float maxZ = minZ + vp.TerrainLength;

            // スポーン地点は S-G = spawnTarget に構造的に一致する。テスト mod は overrideSpawnScenePosition=false かつ
            // gridSizeX/Z が奇数なので spawnTarget は GridCenterWorld = タイル中心になる。
            // The spawn is structurally S-G = spawnTarget. With overrideSpawnScenePosition=false and odd gridSizeX/Z
            // in the test mod, spawnTarget is GridCenterWorld, which is the tile center.
            Assert.That(output.SpawnPoint.x, Is.EqualTo((minX + maxX) * 0.5f).Within(0.01f));
            Assert.That(output.SpawnPoint.z, Is.EqualTo((minZ + maxZ) * 0.5f).Within(0.01f));

            Assert.That(output.MapObjects, Is.Not.Empty);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.That(mapObject.Position.x, Is.InRange(minX, maxX));
                Assert.That(mapObject.Position.z, Is.InRange(minZ, maxZ));

                // 初期カメラとプレイヤーを塞がないよう、全mapObjectの中心をスポーンから15m以上離す
                // Keep every map-object center at least 15m from spawn so it cannot block the player or initial camera
                var distance = new Vector2(mapObject.Position.x - output.SpawnPoint.x, mapObject.Position.z - output.SpawnPoint.z);
                Assert.That(distance.sqrMagnitude, Is.GreaterThanOrEqualTo(15f * 15f));
            }

            AssertVeinsInsideTile(output.ItemVeins, minX, maxX, minZ, maxZ);
            AssertVeinsInsideTile(output.FluidVeins, minX, maxX, minZ, maxZ);
            return output;
        }
        [Test]
        public void スポーン安全域内のMapObjectだけを除外する()
        {
            // 近距離配置だけを除き、境界外の配置順序と座標を維持する
            // Remove only the near placement while preserving the order and position outside the boundary
            var entries = new List<PlacementEntry>
            {
                new PlacementEntry { WorldPosition = new Vector3(3f, 0f, 4f) },
                new PlacementEntry { WorldPosition = new Vector3(20f, 0f, 0f) },
            };
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(entries, Vector3.zero);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].WorldPosition, Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }

        private static void AssertVeinsInsideTile(
            List<PlacedVein> veins, float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(veins, Is.Not.Empty);
            foreach (var vein in veins)
            {
                Assert.That(vein.Min.x, Is.InRange(minX, maxX));
                Assert.That(vein.Max.x, Is.InRange(minX, maxX));
                Assert.That(vein.Min.z, Is.InRange(minZ, maxZ));
                Assert.That(vein.Max.z, Is.InRange(minZ, maxZ));
            }
        }
    }
}
