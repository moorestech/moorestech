using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Tiling;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // クラスタ中心の排他がエントリ内に閉じることを検証する。
    // Verifies cluster-center exclusion stays within each entry.
    public class VeinClusterCenterSeparationTest
    {
        private const string VeinGuidA = "11111111-0000-0000-0000-000000000001";
        private const string VeinGuidB = "11111111-0000-0000-0000-000000000004";
        private const string VeinGuidC = "11111111-0000-0000-0000-000000000003";
        private const float MinimumRelativePlacementRatio = 0.4f;
        private const float DefaultCenterSpacing = 15f;
        private const float TileSize = 250f;
        private const int HeightRes = 65;

        [SetUp]
        public void SetUp()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));
        }

        [Test]
        public void SecondEntryIsNotCrowdedOutByFirstEntry()
        {
            var placements = Generate(
                new[] { CreateEntry(VeinGuidA), CreateEntry(VeinGuidB) }, 0f, CreateHalo(20f), 42);
            int countA = placements.Count(p => p.MapObjectGuid == VeinGuidA);
            int countB = placements.Count(p => p.MapObjectGuid == VeinGuidB);

            // 同一設定なら同数オーダーで湧き、共有グリッドへの退行では後続だけが減る。
            // Identical settings yield the same order of count; a shared-grid regression suppresses only the latter.
            Assert.That(countA, Is.GreaterThan(0));
            Assert.That(countB, Is.GreaterThan(0));
            int smaller = System.Math.Min(countA, countB);
            int larger = System.Math.Max(countA, countB);
            Assert.That((float)smaller / larger, Is.GreaterThanOrEqualTo(MinimumRelativePlacementRatio),
                $"countA={countA} countB={countB}");
        }

        [Test]
        public void EntryMaximumSpacingDoesNotExpandToAnotherEntryMaximum()
        {
            var entryA = CreateEntry(VeinGuidA);
            entryA.bands = new[] { CreateBand(120f, 4f, 100f), CreateBand(-1f, 2f, 100f) };
            var entryB = CreateEntry(VeinGuidB);
            entryB.bands = new[] { CreateBand(-1f, 12f, 100f) };
            var halo = CreateHalo(40f);

            // 複数bandの実中心間隔を検証。
            // Verifies actual center spacing across bands.
            Generate(new[] { entryA, entryB }, 0f, halo, 42);
            var centers = ReadCenters(halo, 0, 0f);
            Assert.That(centers.Count, Is.GreaterThan(1));
            float minimumDistance = MinimumPairDistance();

            Assert.That(minimumDistance, Is.GreaterThanOrEqualTo(10f));
            Assert.That(minimumDistance, Is.LessThan(30f));

            #region Internal

            float MinimumPairDistance()
            {
                float minimum = float.PositiveInfinity;
                for (int first = 0; first < centers.Count; first++)
                    for (int second = first + 1; second < centers.Count; second++)
                        minimum = Mathf.Min(minimum, Vector2.Distance(centers[first], centers[second]));
                return minimum;
            }

            #endregion
        }

        [Test]
        public void AdjacentTileSeparatesOnlyTheSeededEntry()
        {
            // エントリ配列の位置がチャネル鍵。Bが0番、Aが1番になる。
            // The slot in the entry array is the channel key, so B is 0 and A is 1.
            const int entryIndexB = 0;
            const int entryIndexA = 1;
            var entries = new[] { CreateEntry(VeinGuidB), CreateEntry(VeinGuidA) };
            var probeHalo = CreateHalo(20f);
            Generate(entries, TileSize, probeHalo, 43);
            var probeA = ReadCenters(probeHalo, entryIndexA, TileSize);
            var probeB = ReadCenters(probeHalo, entryIndexB, TileSize);

            // 隣タイル内の既知候補から、境界外の同エントリ中心と別エントリ中心の距離証人を固定する。
            // Derives a fixed witness between a same-entry center outside the seam and a different-entry center inside.
            var sameEntryCenter = probeA.First(point => point.x < DefaultCenterSpacing - 1f &&
                probeB.Any(other => Vector2.Distance(other, new Vector2(-1f, point.y)) < DefaultCenterSpacing));
            var seededCenter = new Vector2(-1f, sameEntryCenter.y);
            var otherEntryCenter = probeB.First(point => Vector2.Distance(point, seededCenter) < DefaultCenterSpacing);
            var seededHalo = CreateHalo(20f);
            seededHalo.ItemVeins.Centers.GetOrCreate(entryIndexA).Add(TileSize + seededCenter.x, seededCenter.y);

            Generate(entries, TileSize, seededHalo, 43);
            var generatedA = ReadCenters(seededHalo, entryIndexA, TileSize).Where(point => 0f <= point.x).ToList();
            var generatedB = ReadCenters(seededHalo, entryIndexB, TileSize);

            Assert.That(generatedA.All(point => DefaultCenterSpacing <= Vector2.Distance(point, seededCenter)), Is.True);
            Assert.That(generatedB.Any(point => Vector2.Distance(point, otherEntryCenter) < 0.001f), Is.True);
            Assert.That(Vector2.Distance(otherEntryCenter, seededCenter), Is.LessThan(DefaultCenterSpacing));
        }

        [Test]
        public void 同一veinGuidの2エントリは互いの中心を締め出さない()
        {
            // veinGuidを鍵にしていた頃はこの構成が成立しなかった（マスタ検証が重複を拒否していた）。
            // Back when the key was the veinGuid this configuration was rejected outright by master validation.
            var halo = CreateHalo(20f);
            var placements = Generate(
                new[] { CreateEntry(VeinGuidA), CreateEntry(VeinGuidA) }, 0f, halo, 42);

            Assert.That(placements.Count(p => p.MapObjectGuid == VeinGuidA), Is.GreaterThan(0));

            // 2エントリぶんの中心が別チャネルへ入り、片方が空にならない。
            // Each entry's centers land on their own channel, so neither side comes back empty.
            Assert.That(ReadCenters(halo, 0, 0f).Count, Is.GreaterThan(0));
            Assert.That(ReadCenters(halo, 1, 0f).Count, Is.GreaterThan(0));
        }

        [Test]
        public void ThirdEntryWithHalfDensitySurvivesDenseFirstEntries()
        {
            var entries = new[]
            {
                CreateEntryWithDensity(VeinGuidA, 3.6f),
                CreateEntryWithDensity(VeinGuidB, 3.6f),
                CreateEntryWithDensity(VeinGuidC, 1.8f),
            };
            var placements = Generate(entries, 0f, CreateHalo(20f), 42);

            Assert.That(placements.Count(p => p.MapObjectGuid == VeinGuidC), Is.GreaterThan(0),
                "3番手のエントリ（半分密度）が全滅している");

            #region Internal

            OreEntry CreateEntryWithDensity(string veinGuid, float density)
            {
                var entry = CreateEntry(veinGuid);
                entry.bands[0].density = density;
                return entry;
            }

            #endregion
        }

        private static PlacementHaloStore CreateHalo(float radius)
        {
            return new PlacementHaloStore(radius);
        }

        private static List<PlacementEntry> Generate(
            OreEntry[] entries, float worldOffsetX, PlacementHaloStore halo, int seed)
        {
            var masks = entries.Select(_ => CreateFullMask()).ToArray();
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f, worldOffsetX, 0f,
                HeightRes, HeightRes - 1, 0f, 0f, 123, 0f, 0f,
                (int)(worldOffsetX / TileSize), 0, 2, 1);
            // 確定はproductionのcommitへ通す。写経すると確定AABB台帳の更新が抜けて本番と別挙動になる。
            // Confirmation goes through the production commit; a hand-copied one drops the AABB ledger update and diverges from the real run.
            var placement = OrePlacementGenerator.GenerateForWorld(
                entries, masks, 0f, new float[HeightRes, HeightRes], dims, new System.Random(seed),
                null, null, halo.ItemVeins, halo.Radius,
                halo.CreateConfirmedVeinSnapshot(worldOffsetX, 0f, TileSize, TileSize));
            halo.CommitVeins(halo.ItemVeins, placement);
            return placement.Clusters.SelectMany(cluster => cluster.Members).ToList();

            #region Internal

            bool[,] CreateFullMask()
            {
                var mask = new bool[HeightRes, HeightRes];
                for (int z = 0; z < HeightRes; z++)
                    for (int x = 0; x < HeightRes; x++)
                        mask[z, x] = true;
                return mask;
            }

            #endregion
        }

        private static OreEntry CreateEntry(string veinGuid)
        {
            return new OreEntry
            {
                veinGuid = veinGuid,
                biomes = BiomeFlags.Grassland,
                useSlopeFilter = false,
                minDistanceFromOthers = 0f,
                bands = new[] { CreateBand(-1f, 6f, 3f) },
            };
        }

        private static OreBand CreateBand(float outerRadius, float clusterRadius, float density)
        {
            return new OreBand
            {
                outerRadiusMeters = outerRadius,
                density = density,
                maxObjectsPerCluster = 1,
                clusterRadius = clusterRadius,
                minDistanceBetweenOres = 0f,
                placementRetries = 10,
            };
        }

        // 中心haloはエントリ配列上の位置で引く。同じveinGuidの2エントリを別チャネルとして読み分けられる。
        // Center haloes are looked up by the entry's slot, so two entries sharing a veinGuid read back as separate channels.
        private static List<Vector2> ReadCenters(PlacementHaloStore halo, int entryIndex, float worldOffsetX)
        {
            var grid = new SpatialGrid(TileSize, TileSize, 5f);
            halo.ItemVeins.Centers.GetOrCreate(entryIndex).SeedGrid(
                grid, worldOffsetX, 0f, TileSize, TileSize, halo.Radius);
            return grid.GetAllPoints();
        }

    }
}
