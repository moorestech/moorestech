using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceDisplayBoundaryOwnerTest
    {
        [TestCase(false, -12f)]
        [TestCase(true, 12f)]
        public void CopiesFullOwnerEvaluationAcrossEdgesAndCornerRegardlessOfRequestOrder(bool reverse, float amount)
        {
            var grid = SurfaceGridFixture.Create(2, 17, 31.7f, 47.3f, true);
            var config = grid.Config;
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[] { new TreePrototypeEntry
                {
                    mapObjectGuids = new[] { "boundary-tree" }, heightModAmount = amount, heightModWidth = 8f,
                } },
            };
            var ledger = new PlacementLedger();
            ledger.Add(new LedgerPlacement("boundary-tree", new Vector3(-config.terrainWidth / 32f, 0f, config.terrainLength / 32f),
                Vector3.one, TerrainSurroundEffectType.rockNoBareGround, null));
            ledger.AddGroundingPad(new VeinGroundingPad(new Rect(-2f, -2f, 4f, 4f),
                SurfaceQuantization.PadHeight(20, config, "test"), 2f));
            var source = new DistinctTileSource(17);
            var owner = new SurfaceDisplayBoundaryOwner(config, source);
            var actual = new float[4][,];

            // 非所有タイルを先に要求しても所有者使用
            // Request non-owners first and still use the owner
            for (int step = 0; step < 4; step++)
            {
                int tile = reverse ? 3 - step : step;
                var scene = config.TileScenePosition(tile % 2, tile / 2);
                actual[tile] = new float[17, 17];
                actual[tile][8, 8] = -7f;
                owner.CopyTo(actual[tile], new Vector3(scene.x, 0f, scene.y), ledger,
                    new GroundedSurfaceHeightPolicy(grid.Land, SurfaceEnvelope.GeneratedV5));
                Assert.That(actual[tile][8, 8], Is.EqualTo(-7f), "Only perimeter samples may be copied");
            }
            foreach (int loads in source.LoadCounts) Assert.That(loads, Is.EqualTo(1));

            // 独立フルチェーンと全辺・四枚角を比較
            // Compare every edge and corner with an independent full chain
            var expectedTiles = new[] { Expected(0, 0), Expected(1, 0), Expected(0, 1), Expected(1, 1) };
            for (int tile = 0; tile < 4; tile++)
            for (int z = 0; z < 17; z++)
            for (int x = 0; x < 17; x++)
            {
                if (x != 0 && z != 0 && x != 16 && z != 16) continue;
                int gx = tile % 2 * 16 + x;
                int gz = tile / 2 * 16 + z;
                int ox = Mathf.Max(0, (gx - 1) / 16);
                int oz = Mathf.Max(0, (gz - 1) / 16);
                var expected = expectedTiles[oz * 2 + ox];
                Assert.That(actual[tile][z, x], Is.EqualTo(expected[gz - oz * 16, gx - ox * 16]));
            }
            Assert.That(actual[0][16, 16], Is.EqualTo(actual[3][0, 0]));

            #region Internal
            float[,] Expected(int x, int z)
            {
                var scene = config.TileScenePosition(x, z);
                var position = new Vector3(scene.x, 0f, scene.y);
                var tileConfig = config.CreateTileConfig(x, z);
                var post = TreePerturbationApplier.Apply(new DistinctTileSource(17).Load(x, z), tileConfig, position, ledger.Placements);
                return FinalSurfaceProjector.Apply(post, tileConfig, position, grid.Land, ledger.GroundingPads, SurfaceEnvelope.GeneratedV5);
            }
            #endregion
        }

        private sealed class DistinctTileSource : ISurfaceDisplayHeightSource
        {
            private readonly int _resolution;
            internal readonly int[] LoadCounts = new int[4];

            internal DistinctTileSource(int resolution) { _resolution = resolution; }

            public float[,] Load(int tileX, int tileZ)
            {
                LoadCounts[tileZ * 2 + tileX]++;
                var heights = new float[_resolution, _resolution];
                for (int z = 0; z < _resolution; z++)
                for (int x = 0; x < _resolution; x++)
                    heights[z, x] = 0.01f + tileX * 0.02f + tileZ * 0.03f;
                return heights;
            }
        }
    }
}
