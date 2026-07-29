using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Pipeline.Stages;
using Mooresmaster.Model.GenerationModule;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 中央化オフセット G をノイズ座標にだけ効かせ、出力(スポーン・配置物・鉱脈)は -G でシーン座標へ戻ることを検証する。
    // 探索成功時も失敗時も、生成された単一タイル [0,terrainWidth]x[0,terrainLength] の内側に収まらねばならない。
    // Verifies the centering offset G applies only to noise coordinates while outputs (spawn, placements, veins)
    // come back to scene space via -G, landing inside the single generated tile in both success and fallback cases.
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
        }

        [Test]
        public void SpawnAndPlacementsAreInsideTileWhenSpawnSearchFallsBack()
        {
            var generation = TestGenerationConfigFactory.Create(
                TestGenerationConfigFactory.SpawnSearchSetup.Unsatisfiable);

            var searchResult = FindSpawnRegion(generation);
            Assert.That(searchResult.Success, Is.False);

            AssertOutputIsInsideTile(generation);
        }

        private static SpawnSearchResult FindSpawnRegion(Generation generation)
        {
            var config = GenerationRuntimeConfigFactory.Build(generation);
            config.seed = Seed;
            return SpawnRegionFinder.Find(config, ClassificationStage.GetEnabledBiomeTypes(config));
        }

        // スポーン地点のシーン座標をハイトマップ格子へ写し、その画素のバイオームを返す。
        // Maps the spawn scene position onto the heightmap lattice and returns that pixel's biome.
        private static BiomeType BiomeAtSpawn(MapGenerationOutput output, Generation generation)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            int res = output.Resolution;
            int px = Mathf.RoundToInt((output.SpawnPoint.x - vp.WorldOffsetX) / vp.TerrainWidth * (res - 1));
            int pz = Mathf.RoundToInt((output.SpawnPoint.z - vp.WorldOffsetZ) / vp.TerrainLength * (res - 1));
            return (BiomeType)output.BiomeIndices[pz * res + px];
        }

        private static MapGenerationOutput AssertOutputIsInsideTile(Generation generation)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            float maxX = vp.WorldOffsetX + vp.TerrainWidth;
            float maxZ = vp.WorldOffsetZ + vp.TerrainLength;

            var output = MapGenerationPipeline.Generate(generation, Seed);

            // スポーン地点は S-G = spawnTarget（グリッド中心）に構造的に一致するため、タイル中心へ落ちる。
            // The spawn is structurally S-G = spawnTarget (grid center), so it lands at the tile center.
            Assert.That(output.SpawnPoint.x, Is.EqualTo((vp.WorldOffsetX + maxX) * 0.5f).Within(0.01f));
            Assert.That(output.SpawnPoint.z, Is.EqualTo((vp.WorldOffsetZ + maxZ) * 0.5f).Within(0.01f));

            Assert.That(output.MapObjects, Is.Not.Empty);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.That(mapObject.Position.x, Is.InRange(vp.WorldOffsetX, maxX));
                Assert.That(mapObject.Position.z, Is.InRange(vp.WorldOffsetZ, maxZ));
            }

            AssertVeinsInsideTile(output.ItemVeins, vp.WorldOffsetX, maxX, vp.WorldOffsetZ, maxZ);
            AssertVeinsInsideTile(output.FluidVeins, vp.WorldOffsetX, maxX, vp.WorldOffsetZ, maxZ);
            return output;
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
