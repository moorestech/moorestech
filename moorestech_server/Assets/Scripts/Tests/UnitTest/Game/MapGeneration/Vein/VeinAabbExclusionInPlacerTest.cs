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

namespace Tests.UnitTest.Game.MapGeneration.Vein
{
    /// <summary>
    ///     鉱脈AABBの排他がメンバー配置の内側で行われ、落選点が排他グリッドへ幽霊として残らないことを固定する
    ///     Pins that vein AABB exclusion runs inside member placement so rejected points never linger in the exclusion grid as ghosts
    /// </summary>
    public class VeinAabbExclusionInPlacerTest
    {
        private const string VeinGuidA = "11111111-0000-0000-0000-000000000001";
        private const string VeinGuidB = "11111111-0000-0000-0000-000000000004";
        private const float TileSize = 250f;
        private const int HeightRes = 65;

        [SetUp]
        public void SetUp()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));
        }

        [Test]
        public void 除外AABBに重なる候補はメンバーにも確定AABBにも入らず全滅した中心は出力されない()
        {
            var everywhere = new List<PlacedVein>
            {
                new() { VeinGuid = "excluded", Min = new Vector3Int(-10000, -10000, -10000), Max = new Vector3Int(10000, 10000, 10000) },
            };

            var placement = Generate(new[] { CreateEntry(VeinGuidA, 1, 0f, 0f) }, everywhere, 42);

            Assert.AreEqual(0, placement.Veins.Count);
            Assert.AreEqual(0, placement.Clusters.Count, "メンバーが全滅した中心は出力されない");
        }

        [Test]
        public void 除外で落ちた点は排他グリッドに残らず後続エントリがその近傍へ置ける()
        {
            // 先にAだけ置いて位置を取り、その全点を除外AABBにした状態でA→Bを置く。Aは全滅し、Bは除外点の近傍(4セル未満)へ届く
            // Place A alone to learn its points, then exclude them all and place A then B; A dies out while B still reaches within 4 cells of those points
            var baseline = Generate(new[] { CreateEntry(VeinGuidA, 1, 0f, 4f) }, new List<PlacedVein>(), 42);
            var excluded = baseline.Veins.Select(vein => new PlacedVein { VeinGuid = vein.VeinGuid, Min = vein.Min, Max = vein.Max }).ToList();
            Assert.That(excluded.Count, Is.GreaterThan(10));

            var placement = Generate(new[] { CreateEntry(VeinGuidA, 1, 0f, 4f), CreateEntry(VeinGuidB, 1, 0f, 4f) }, excluded, 42);

            Assert.AreEqual(0, placement.Clusters.Count(cluster => cluster.VeinGuid == VeinGuidA));
            var membersB = placement.Clusters.Where(cluster => cluster.VeinGuid == VeinGuidB).SelectMany(cluster => cluster.Members).ToList();
            Assert.That(membersB.Count, Is.GreaterThan(0));

            // 幽霊が残るとAの落選点から4セル未満のBは全て弾かれる。AABB(±1)非重なりかつ4セル未満の点が1つでもあれば幽霊は居ない
            // With ghosts every B within 4 cells of a rejected A point is blocked; one B point that clears the AABB (±1) yet sits under 4 cells proves there is none
            var witnesses = membersB.Count(member => excluded.Any(vein =>
            {
                var center = (Vector3)(vein.Min + vein.Max) / 2f;
                var dx = Mathf.Abs(member.WorldPosition.x - center.x);
                var dz = Mathf.Abs(member.WorldPosition.z - center.z);
                return 2f < Mathf.Max(dx, dz) && Mathf.Sqrt(dx * dx + dz * dz) < 4f;
            }));
            Assert.That(witnesses, Is.GreaterThan(0), "除外で落ちた点が排他グリッドに幽霊として残っている");
        }

        [Test]
        public void 同一バッチ内で確定した鉱脈同士のAABBは重ならない()
        {
            // 最小距離0・半径1の密なクラスターでは、同バッチ排他が無ければ隣接セルのメンバーが重なる
            // With zero minimum distance and radius 1 clusters, adjacent-cell members would overlap without same-batch exclusion
            var placement = Generate(new[] { CreateEntry(VeinGuidA, 6, 1f, 0f) }, new List<PlacedVein>(), 7);

            Assert.That(placement.Veins.Count, Is.GreaterThan(10));
            for (int i = 0; i < placement.Veins.Count; i++)
                for (int j = i + 1; j < placement.Veins.Count; j++)
                    Assert.IsFalse(VeinAabbBuilder.OverlapsAny(placement.Veins[i], new[] { placement.Veins[j] }),
                        $"vein[{i}] {placement.Veins[i].Min}-{placement.Veins[i].Max} と vein[{j}] {placement.Veins[j].Min}-{placement.Veins[j].Max} が重なる");
        }

        private static VeinPlacementBatch Generate(OreEntry[] entries, IReadOnlyList<PlacedVein> excludedVeins, int seed)
        {
            var halo = new PlacementHaloStore(20f);
            var masks = entries.Select(_ => CreateFullMask()).ToArray();
            var dims = new TerrainDimensions(
                TileSize, TileSize, 100f, 0f, 0f,
                HeightRes, HeightRes - 1, 0f, 0f, 123, 0f, 0f,
                0, 0, 2, 1);
            return OrePlacementGenerator.GenerateForWorld(
                entries, masks, 0f, new float[HeightRes, HeightRes], dims, new System.Random(seed),
                null, null, halo.ItemVeinMembers, halo.ItemVeinCenters, halo.Radius, excludedVeins);

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

        private static OreEntry CreateEntry(string veinGuid, int maxObjectsPerCluster, float clusterRadius, float minDistanceBetweenOres)
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
                        maxObjectsPerCluster = maxObjectsPerCluster,
                        clusterRadius = clusterRadius,
                        minDistanceBetweenOres = minDistanceBetweenOres,
                        placementRetries = 10,
                    },
                },
            };
        }
    }
}
