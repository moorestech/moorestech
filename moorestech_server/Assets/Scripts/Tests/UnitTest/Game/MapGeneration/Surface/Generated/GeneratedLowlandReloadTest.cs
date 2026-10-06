using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public class GeneratedLowlandReloadTest
    {
        private string _scratch;

        [TearDown]
        public void TearDown()
        {
            if (_scratch != null && Directory.Exists(_scratch)) Directory.Delete(_scratch, true);
        }

        [Test]
        [Timeout(1500000)]
        public void DeliberateBelowSeaLandAndNegativeTreeSurviveSavedHeightReload()
        {
            var grid = SurfaceGridFixture.Create(1, 33, 32f, 32f, true);
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) grid.SetHeight(x, z, 3f);
            var root = Path.Combine(Path.GetTempPath(), "vtg-lowland-" + Guid.NewGuid().ToString("N"));
            _scratch = root;
            var saved = WorldDataDirectory.FromWorldRoot(root);

            // 故意の低地を本番r16ライタとローダーで往復する
            // Round-trip deliberately low land through the production r16 writer and loader
            TerrainFileWriter.Write(saved, grid.Output);
            var loaded = HeightFileLoader.LoadHeights(saved, 0, 0, 33);
            var input = new float[33 * 33];
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) input[z * 33 + x] = loaded[z, x];
            var entry = PlacementEntry.CreateTree("negative-tree", new Vector3(16f, 3f, 16f),
                Quaternion.identity, Vector3.one, 0f, TerrainSurroundEffectType.rockNoBareGround);
            var modifiers = new Dictionary<string, (float amount, float width)> { { "negative-tree", (-12f, 4f) } };
            TreeHeightModifier.Apply(input, grid.Config, new List<PlacementEntry> { entry }, modifiers);
            Assert.That(input[16 * 33 + 16] * 600f, Is.LessThan(0f), "Negative tree fixture must actually depress land");

            var postTree = new float[33, 33];
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) postTree[z, x] = input[z * 33 + x];
            var final = FinalSurfaceProjector.Apply(postTree, grid.Config, Vector3.zero, grid.Land,
                Array.Empty<VeinGroundingPad>(), SurfaceEnvelope.GeneratedV5);
            float required = SurfaceQuantization.LandFloor(grid.Config, SurfaceEnvelope.GeneratedV5, "fixture");

            // 木加工後の本番floorを通し保存再ロードでも下限を維持する
            // Apply the production post-tree floor and retain its lower bound after saved reload
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) grid.Output.Tiles[0].Heights[z * 33 + x] = final[z, x];
            TerrainFileWriter.Write(saved, grid.Output);
            var reloaded = HeightFileLoader.LoadHeights(saved, 0, 0, 33);
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
            {
                Assert.That(SurfaceQuantization.StoredNormalized(reloaded[z, x]) * 600f, Is.GreaterThanOrEqualTo(required));
                Assert.That(reloaded[z, x], Is.EqualTo(final[z, x]));
            }
            TestContext.WriteLine("Synthetic lowland regression; sea exposure report is not reproduced by this fixture.");
        }
    }
}
