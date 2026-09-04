using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Visual;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using Tests.UnitTest.Game.MapGeneration.Tiling.Seam;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    // 可視化パス側の多バイオームタイル境界一致を検証
    // 生成パス(VanillaGenerator)側は TileBoundarySeamTest が既に見ており、本テストはその対の可視化パス側を埋める。
    // Verifies multi-biome tile-border agreement on the visualization path
    // The generation path (VanillaGenerator) is already covered by TileBoundarySeamTest; this fills its visualization-path counterpart.
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap3")]
    public class TileVisualBakerBoundaryTest
    {
        private const int GridSide = 3;
        private const int Seed = 42;

        [Test]
        public void 可視化パスの分類はタイル境界で一致する()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var output = new VanillaGenerator().Generate(config).Output;
            Assert.AreEqual(GridSide * GridSide, output.Tiles.Count);

            var visualBiomeIndicesByTile = ComputeVisualizationPathBiomeIndices(config, output);

            var distinctBiomes = new HashSet<byte>();
            foreach (var biomeIndices in visualBiomeIndicesByTile.Values)
            foreach (var biomeIndex in biomeIndices)
                distinctBiomes.Add(biomeIndex);
            Assert.Less(1, distinctBiomes.Count, "バイオームが1種しか出ておらず境界の比較が空になる");

            var resolution = config.Resolution;
            var mismatches = new List<string>();
            for (var z = 0; z < GridSide; z++)
            for (var x = 0; x < GridSide; x++)
            {
                var coord = new Vector2Int(x, z);
                var biomeIndices = visualBiomeIndicesByTile[coord];
                if (x + 1 < GridSide) CollectColumnMismatches(coord, new Vector2Int(x + 1, z), biomeIndices, visualBiomeIndicesByTile[new Vector2Int(x + 1, z)], resolution, mismatches);
                if (z + 1 < GridSide) CollectRowMismatches(coord, new Vector2Int(x, z + 1), biomeIndices, visualBiomeIndicesByTile[new Vector2Int(x, z + 1)], resolution, mismatches);
            }

            Assert.AreEqual(0, mismatches.Count, $"visual biome seam count={mismatches.Count} sample={(mismatches.Count > 0 ? mismatches[0] : "(none)")}");
        }

        [Test]
        public void 可視化パスの分類は生成パスの分類と全画素で一致する()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var output = new VanillaGenerator().Generate(config).Output;

            var visualBiomeIndicesByTile = ComputeVisualizationPathBiomeIndices(config, output);
            var generationBiomeIndicesByTile = TileBiomeIndexComputer.ComputeForAllTiles(config, output);

            foreach (var coord in visualBiomeIndicesByTile.Keys)
                CollectionAssert.AreEqual(
                    generationBiomeIndicesByTile[coord], visualBiomeIndicesByTile[coord],
                    $"tile ({coord.x},{coord.y}) の可視化パスと生成パスの分類が一致しない");
        }

        // TileVisualBaker.BuildAlphamap と同じ分類経路（TileClassificationContext）を通し、flat比較のため詰め替えだけ手元で行う
        // Runs the same classification path as TileVisualBaker.BuildAlphamap (TileClassificationContext), keeping the flat packing local for comparison
        private static Dictionary<Vector2Int, byte[]> ComputeVisualizationPathBiomeIndices(TerrainGenerationConfig config, MapGenerationOutput output)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;

            var resolution = config.Resolution;
            var result = new Dictionary<Vector2Int, byte[]>();
            foreach (var tile in output.Tiles)
            {
                var tileConfig = gridConfig.CreateTileConfig(tile.TileX, tile.TileZ);
                using var classification = new TileClassificationContext(tileConfig, biomeTypes);
                classification.Initialize();

                var biomeIndices = PlacementInputBuilder.BuildBiomeIndices(
                    classification.Buffers.winnerBiomeIndex, classification.Buffers.landMask, classification.Buffers.beachFactor,
                    biomeTypes, resolution * resolution);
                result[new Vector2Int(tile.TileX, tile.TileZ)] = biomeIndices;
            }

            return result;
        }

        private static void CollectColumnMismatches(
            Vector2Int leftCoord, Vector2Int rightCoord, byte[] leftBiomeIndices, byte[] rightBiomeIndices,
            int resolution, List<string> mismatches)
        {
            for (var z = 0; z < resolution; z++)
            {
                var leftIndex = z * resolution + (resolution - 1);
                var rightIndex = z * resolution;
                if (leftBiomeIndices[leftIndex] != rightBiomeIndices[rightIndex])
                    mismatches.Add($"{leftCoord}->{rightCoord} z={z}: {leftBiomeIndices[leftIndex]} vs {rightBiomeIndices[rightIndex]}");
            }
        }

        private static void CollectRowMismatches(
            Vector2Int nearCoord, Vector2Int farCoord, byte[] nearBiomeIndices, byte[] farBiomeIndices,
            int resolution, List<string> mismatches)
        {
            for (var x = 0; x < resolution; x++)
            {
                var nearIndex = (resolution - 1) * resolution + x;
                var farIndex = x;
                if (nearBiomeIndices[nearIndex] != farBiomeIndices[farIndex])
                    mismatches.Add($"{nearCoord}->{farCoord} x={x}: {nearBiomeIndices[nearIndex]} vs {farBiomeIndices[farIndex]}");
            }
        }
    }
}
