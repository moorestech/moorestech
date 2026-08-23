using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Master;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Tiling;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration
{
    // クラスタ中心の排他がエントリ内に閉じることを検証する。共有グリッドだと先行エントリが後続を面で締め出す（2026-08-23のバグ）。
    // Verifies cluster-center exclusion stays within an entry; a shared grid let the first entry blanket later ones out (2026-08-23 bug).
    public class VeinClusterCenterSeparationTest
    {
        private const string VeinGuidA = "11111111-0000-0000-0000-000000000001";
        private const string VeinGuidB = "11111111-0000-0000-0000-000000000004";
        private const string VeinGuidC = "11111111-0000-0000-0000-000000000003";
        private const float TileSize = 250f;
        private const int HeightRes = 65;

        [Test]
        public void SecondEntryIsNotCrowdedOutByFirstEntry()
        {
            // OreEntryPlacerがveinGuidでmapVeinsマスタを引くため先にロードする
            // Load masters first because OreEntryPlacer resolves veinGuid against the mapVeins master
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            var placements = Generate(worldOffsetX: 0f, halo: CreateHalo());

            int countA = placements.Count(p => p.MapObjectGuid == VeinGuidA);
            int countB = placements.Count(p => p.MapObjectGuid == VeinGuidB);

            // 同一設定の2エントリは同数オーダーで湧くはず。共有グリッド実装ではcountBがほぼ0になる。
            // Two identical entries should yield the same order of counts; the shared-grid code drives countB to near zero.
            Assert.That(countA, Is.GreaterThan(0));
            Assert.That(countB, Is.GreaterThan(0));
            int smaller = System.Math.Min(countA, countB);
            int larger = System.Math.Max(countA, countB);
            Assert.That(smaller, Is.GreaterThanOrEqualTo(larger * 4 / 10),
                $"countA={countA} countB={countB}");
        }

        [Test]
        public void AdjacentTileWithSeededHaloStillPlacesBothEntries()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // タイル0で溜めた中心haloを隣接タイル1に効かせても、エントリ間の締め出しが起きないこと。
            // Even with tile 0's center haloes seeded into adjacent tile 1, no cross-entry crowd-out occurs.
            var halo = CreateHalo();
            Generate(worldOffsetX: 0f, halo: halo);
            var secondTile = Generate(worldOffsetX: TileSize, halo: halo);

            Assert.That(secondTile.Count(p => p.MapObjectGuid == VeinGuidA), Is.GreaterThan(0));
            Assert.That(secondTile.Count(p => p.MapObjectGuid == VeinGuidB), Is.GreaterThan(0));
        }

        [Test]
        public void ThirdEntryWithHalfDensitySurvivesDenseFirstEntries()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // 実マスタと同じ構図: 高密度2エントリの後に半分密度のエントリ。共有グリッドでは3番手が全滅していた。
            // Mirrors the live master: two dense entries then a half-density one; the shared grid wiped the third out.
            var entries = new[]
            {
                CreateEntryWithDensity(VeinGuidA, 3.6f),
                CreateEntryWithDensity(VeinGuidB, 3.6f),
                CreateEntryWithDensity(VeinGuidC, 1.8f),
            };
            var entryMasks = new[] { CreateFullMask(), CreateFullMask(), CreateFullMask() };
            var heights = new float[HeightRes, HeightRes];
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f, 0f, 0f,
                HeightRes, 0f, 0f, 123, 0f, 0f, 0, 0, 1, 1);
            var halo = CreateHalo();

            var placements = OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, 0f, heights, dims, new System.Random(42),
                null, null, halo.ItemVeinMembers, halo.ItemVeinCenters, halo.Radius);

            Assert.That(placements.Count(p => p.MapObjectGuid == VeinGuidC), Is.GreaterThan(0),
                "3番手のエントリ（半分密度）が全滅している");
        }

        #region Internal

        static PlacementHaloStore CreateHalo()
        {
            // 半径はテスト設定の全制約最大（clusterRadius6*2.5=15）を上回る値で固定
            // Fixed above the largest constraint in this test setup (clusterRadius 6 * 2.5 = 15)
            return new PlacementHaloStore(20f);
        }

        static List<PlacementEntry> Generate(float worldOffsetX, PlacementHaloStore halo)
        {
            var entries = new[] { CreateEntry(VeinGuidA), CreateEntry(VeinGuidB) };
            var entryMasks = new[] { CreateFullMask(), CreateFullMask() };
            var heights = new float[HeightRes, HeightRes];
            int tileIndexX = (int)(worldOffsetX / TileSize);
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f,
                worldOffsetX, 0f,
                HeightRes, 0f, 0f, 123,
                0f, 0f,
                tileIndexX, 0, 2, 1);
            var rng = new System.Random(42 + tileIndexX);

            return OrePlacementGenerator.GenerateForWorld(
                entries, entryMasks, 0f, heights, dims, rng,
                null, null,
                halo.ItemVeinMembers, halo.ItemVeinCenters, halo.Radius);
        }

        static OreEntry CreateEntry(string veinGuid)
        {
            return new OreEntry
            {
                veinGuid = veinGuid,
                biomes = BiomeFlags.Grassland,
                useSlopeFilter = false,
                minDistanceFromOthers = 0f,
                bands = new[]
                {
                    new OreBand
                    {
                        outerRadiusMeters = -1f,
                        density = 3f,
                        maxObjectsPerCluster = 1,
                        clusterRadius = 6f,
                        minDistanceBetweenOres = 0f,
                        placementRetries = 10,
                    },
                },
            };
        }

        static OreEntry CreateEntryWithDensity(string veinGuid, float density)
        {
            var entry = CreateEntry(veinGuid);
            entry.bands[0].density = density;
            return entry;
        }

        static bool[,] CreateFullMask()
        {
            var mask = new bool[HeightRes, HeightRes];
            for (int z = 0; z < HeightRes; z++)
                for (int x = 0; x < HeightRes; x++)
                    mask[z, x] = true;
            return mask;
        }

        #endregion
    }
}
