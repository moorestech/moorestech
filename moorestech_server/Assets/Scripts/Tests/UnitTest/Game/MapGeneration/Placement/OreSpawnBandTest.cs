using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 鉱脈のスポーン距離帯が帯の実体でリングへ対応することを固定するテスト。
    // Pins that a vein's spawn-distance bands map onto rings by band identity.
    public class OreSpawnBandTest
    {
        private const int Seed = 11;
        private const float NearRadius = 120f;

        // 鉱脈はクラスタ中心でリング判定し、実体はワールド整数座標へ丸めて置かれるため半マス強ずれる。
        // The ring test runs on the cluster centre while the ore itself snaps to integer world coordinates, shifting it by half a block.
        private const float IntegerSnapTolerance = 1.5f;

        // 宣言順を外半径の降順にしても、密度を持つ近傍帯が近傍リングへ効く（添字前提だと最外周と入れ替わる）。
        // Declared in descending radius order, the near band with density still drives the near ring; an index-based mapping would swap it with the outermost.
        [Test]
        public void 降順に宣言しても近傍帯だけ密度を持つ鉱脈は近傍半径未満にのみ置かれる()
        {
            var output = GenerateVeins(
                CenterOnlyBand(-1f, density: 0f),
                CenterOnlyBand(NearRadius, density: 40f));

            Assert.IsNotEmpty(output.ItemVeins);
            foreach (var vein in output.ItemVeins)
                Assert.Less(DistanceFromSpawnXz(vein, output.SpawnPoint), NearRadius + IntegerSnapTolerance);
        }

        // density 0 の帯は「置かない」宣言。間隔クランプで拾って湧かせないことを固定する。
        // A zero-density band declares "place nothing"; this pins that the spacing clamp does not spawn from it anyway.
        [Test]
        public void 最外周だけ密度を持つ鉱脈は近傍半径以上にのみ置かれる()
        {
            var output = GenerateVeins(
                CenterOnlyBand(NearRadius, density: 0f),
                CenterOnlyBand(-1f, density: 40f));

            Assert.IsNotEmpty(output.ItemVeins);
            foreach (var vein in output.ItemVeins)
                Assert.GreaterOrEqual(DistanceFromSpawnXz(vein, output.SpawnPoint), NearRadius - IntegerSnapTolerance);
        }

        // クラスタ半径0・1個限定にして、リング判定の対象であるクラスタ中心そのものを検査対象にする。
        // A zero-radius single-member cluster makes the placed ore stand for the cluster centre the ring test actually uses.
        private static OreBand CenterOnlyBand(float outerRadiusMeters, float density)
        {
            return new OreBand
            {
                outerRadiusMeters = outerRadiusMeters,
                density = density,
                maxObjectsPerCluster = 1,
                clusterRadius = 0f,
                minDistanceBetweenOres = 0f,
            };
        }

        private static MapGenerationOutput GenerateVeins(params OreBand[] bands)
        {
            var config = MultiTileTestWorld.BuildConfig(gridSide: 1, Seed);
            config.generateOre = true;
            config.oreConfig = new WorldOreConfig
            {
                entries = new[]
                {
                    new OreEntry
                    {
                        veinGuid = TestGenerationConfigFactory.TestVeinGuid,
                        biomes = BiomeFlags.Grassland | BiomeFlags.Forest,
                        useSlopeFilter = false,
                        minDistanceFromOthers = 0f,
                        bands = bands,
                    },
                },
            };
            return new VanillaGenerator().Generate(config);
        }

        // 鉱脈AABBの中心をスポーンXZ距離へ直す。
        // Converts the vein AABB's centre into an XZ distance from spawn.
        private static float DistanceFromSpawnXz(PlacedVein vein, Vector3 spawn)
        {
            var center = new Vector2((vein.Min.x + vein.Max.x) * 0.5f, (vein.Min.z + vein.Max.z) * 0.5f);
            return Vector2.Distance(center, new Vector2(spawn.x, spawn.z));
        }
    }
}
