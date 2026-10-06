using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Surface.Placement;
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
    // 鉱脈中心の複数ケースで使う生成土台
    // Shared generation fixture for vein-center scenarios
    internal static class VeinClusterTestFixtures
    {
        internal const float TileSize = 250f;
        internal const int HeightRes = 65;
        internal static PlacementHaloStore CreateHalo(float radius)
        {
            return new PlacementHaloStore(radius);
        }

        internal static List<PlacementEntry> Generate(
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
                halo.CreateConfirmedVeinSnapshot(TileCandidateAabbBounds.From(dims)), new UnrestrictedVeinLandConstraint(), (int)(worldOffsetX / TileSize), 0);
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

        internal static OreEntry CreateEntry(string veinGuid)
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

        internal static OreBand CreateBand(float outerRadius, float clusterRadius, float density)
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
        internal static List<Vector2> ReadCenters(PlacementHaloStore halo, int entryIndex, float worldOffsetX)
        {
            var grid = new SpatialGrid(TileSize, TileSize, 5f);
            halo.ItemVeins.Centers.GetOrCreate(entryIndex).SeedGrid(
                grid, worldOffsetX, 0f, TileSize, TileSize, halo.Radius);
            return grid.GetAllPoints();
        }

    }
}
