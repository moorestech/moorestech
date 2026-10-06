using System;
using System.IO;
using Game.MapGeneration.Export;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class StoredSurfaceDisplayBoundaryTest
    {
        private WorldDataDirectory _directory;

        [TearDown]
        public void TearDown()
        {
            if (_directory != null && Directory.Exists(_directory.Root)) Directory.Delete(_directory.Root, true);
        }

        [TestCase(-12f)]
        [TestCase(12f)]
        public void SavedR16OwnerAndGeneratedOwnerProduceIdenticalBoundariesInReverseOrder(float amount)
        {
            var grid = SurfaceGridFixture.Create(2, 17, 31.7f, 47.3f, true);
            foreach (var tile in grid.Output.Tiles)
                for (int i = 0; i < tile.Heights.Length; i++) tile.Heights[i] = 0.01234567f;
            grid.Config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[] { new TreePrototypeEntry
                {
                    mapObjectGuids = new[] { "seam-tree" }, heightModAmount = amount, heightModWidth = 8f,
                } },
            };
            var ledger = new PlacementLedger();
            ledger.Add(new LedgerPlacement("seam-tree", new Vector3(-grid.Config.terrainWidth / 32f, 0f, 0f),
                Vector3.one, TerrainSurroundEffectType.rockNoBareGround, null));
            ledger.AddGroundingPad(new VeinGroundingPad(new Rect(-2f, -2f, 4f, 4f),
                SurfaceQuantization.PadHeight(20, grid.Config, "stored-test"), 2f));
            _directory = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "vtg-owner-" + Guid.NewGuid().ToString("N")));
            TerrainFileWriter.Write(_directory, grid.Output);
            var generated = new SurfaceDisplayBoundaryOwner(grid.Config, new GeneratedSurfaceDisplayHeightSource(grid));
            var stored = new SurfaceDisplayBoundaryOwner(grid.Config, new StoredSurfaceDisplayHeightSource(_directory, 17));
            var generatedTiles = new float[4][,];

            // 生成側を正順、保存再構築を逆順に要求する
            // Request generation forward and stored reconstruction in reverse
            for (int i = 0; i < 4; i++)
            {
                generatedTiles[i] = new float[17, 17];
                var scene = grid.Config.TileScenePosition(i % 2, i / 2);
                generated.CopyTo(generatedTiles[i], new Vector3(scene.x, 0f, scene.y), ledger,
                    grid.Land, SurfaceEnvelope.GeneratedV5, true);
            }
            for (int i = 3; i >= 0; i--)
            {
                var result = new float[17, 17];
                var scene = grid.Config.TileScenePosition(i % 2, i / 2);
                stored.CopyTo(result, new Vector3(scene.x, 0f, scene.y), ledger,
                    grid.Land, SurfaceEnvelope.GeneratedV5, true);
                CollectionAssert.AreEqual(generatedTiles[i], result);
            }
        }
    }
}
