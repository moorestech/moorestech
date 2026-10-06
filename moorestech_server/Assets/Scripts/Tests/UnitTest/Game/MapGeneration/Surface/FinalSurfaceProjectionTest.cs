using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class FinalSurfaceProjectionTest
    {
        [TestCase(-12f)]
        [TestCase(12f)]
        public void TreeDisplacementCannotBreakLandFloorOrCore(float displacement)
        {
            var grid = SurfaceGridFixture.Create(1, 33, 32f, 32f, true);
            var input = new float[33, 33];
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) input[z, x] = displacement / grid.Config.terrainHeight;
            float padHeight = SurfaceQuantization.PadHeight(20, grid.Config.terrainHeight);
            Assert.That((double)padHeight, Is.LessThanOrEqualTo(20 - 0.001d));
            var pads = new[] { new VeinGroundingPad(new Rect(10f, 10f, 4f, 4f), padHeight, 2f) };
            var post = FinalSurfaceProjector.Apply(input, grid.Config, Vector3.zero, grid.Land, pads);

            // 最終量子化後の低地と平坦支持を検査する
            // Check low land and flat interpolation support after final quantization
            float floor = SurfaceQuantization.LandFloor(grid.Config.terrainHeight, SurfaceEnvelope.GeneratedV5);
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) Assert.That(post[z, x] * grid.Config.terrainHeight, Is.GreaterThanOrEqualTo(floor));
            var support = grid.Geometry.SupportVertices(pads[0].Core);
            for (int z = support.yMin; z < support.yMax; z++)
            for (int x = support.xMin; x < support.xMax; x++)
                Assert.That(post[z, x] * grid.Config.terrainHeight, Is.EqualTo(padHeight));
            Assert.That(input[0, 0], Is.EqualTo(displacement / grid.Config.terrainHeight));
        }

        [Test]
        public void ReanchorRetainsSinkRotationAndDoesNotShiftVeins()
        {
            var before = SurfaceGridFixture.Create(1, 17, 32f, 32f, true);
            var after = SurfaceGridFixture.Create(1, 17, 32f, 32f, true);
            for (int z = 0; z < 17; z++)
            for (int x = 0; x < 17; x++)
            {
                before.SetHeight(x, z, 10f);
                after.SetHeight(x, z, 14f);
            }
            var rotation = Quaternion.Euler(10f, 20f, 30f);
            var output = before.Output;
            output.MapObjects.Add(new PlacedMapObject { Position = new Vector3(12f, 8.5f, 12f), Rotation = rotation });
            output.ItemVeins.Add(new PlacedVein("ore", new Vector3Int(11, 20, 11), new Vector3Int(13, 22, 13)));
            var ledger = new PlacementLedger();
            ledger.Add(new LedgerPlacement("tree", output.MapObjects[0].Position, Vector3.one, TerrainSurroundEffectType.rockNoBareGround, null));
            ledger.Add(new LedgerPlacement("ore", new Vector3(12f, 20.25f, 12f), Vector3.one, TerrainSurroundEffectType.rockNoBareGround, null));
            var bindings = new SurfacePlacementBindings();
            bindings.AddMapObject(0, 0);
            bindings.AddItemVein(0, 1);
            var result = SurfaceObjectReanchor.Apply(output, ledger, bindings, before, after);

            Assert.That(output.MapObjects[0].Position.y, Is.EqualTo(12.5f).Within(0.00001f));
            Assert.That(result.Placements[0].ScenePosition.y, Is.EqualTo(output.MapObjects[0].Position.y));
            Assert.That(output.MapObjects[0].Rotation, Is.EqualTo(rotation));
            Assert.That(output.ItemVeins[0].Min.y, Is.EqualTo(20));
            Assert.That(result.Placements[1].ScenePosition.y, Is.EqualTo(20.25f));
            Assert.That(ledger.Placements[0].ScenePosition.y, Is.EqualTo(8.5f));
        }

        [TestCase(-8f)]
        [TestCase(8f)]
        public void TreeHeightProcessingIgnoresPlacementY(float amount)
        {
            var config = new TerrainGenerationConfig { overrideResolution = 33, terrainWidth = 32f, terrainLength = 32f };
            var mods = new Dictionary<string, (float amount, float width)> { { "tree", (amount, 4f) } };
            var low = new float[33 * 33];
            var high = new float[33 * 33];
            Apply(low, -50f);
            Apply(high, 80f);
            CollectionAssert.AreEqual(low, high);
            Assert.That(low[16 * 33 + 16], Is.EqualTo(amount / config.terrainHeight));

            #region Internal

            void Apply(float[] heights, float y)
            {
                var entry = PlacementEntry.CreateTree("tree", new Vector3(16f, y, 16f), Quaternion.identity,
                    Vector3.one, 0f, TerrainSurroundEffectType.rockNoBareGround);
                TreeHeightModifier.Apply(heights, config, new List<PlacementEntry> { entry }, mods);
            }

            #endregion
        }
    }
}
