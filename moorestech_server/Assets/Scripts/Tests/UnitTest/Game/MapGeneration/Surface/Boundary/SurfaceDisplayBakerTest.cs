using System;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Export;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceDisplayBakerTest
    {
        private WorldDataDirectory _directory;
        private static readonly BiomeType[] Biomes = { BiomeType.Grassland };

        [TearDown]
        public void TearDown()
        {
            if (_directory != null && Directory.Exists(_directory.Root)) Directory.Delete(_directory.Root, true);
        }

        [Test]
        public void ReverseBakesAndMixedCacheHitsKeepExactSharedEdgesAfterReload()
        {
            var grid = SurfaceGridFixture.Create(2, 17, 31.7f, 47.3f, true);
            var config = grid.Config;
            config.landThreshold = -1f;
            config.detailResolution = 8;
            config.generateDetail = false;
            config.forestEnabled = config.savannaEnabled = config.desertEnabled = config.mesaEnabled = false;
            config.alpineEnabled = config.jungleEnabled = config.woodsEnabled = false;
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[] { new TreePrototypeEntry
                {
                    mapObjectGuids = new[] { "baker-seam-tree" }, heightModAmount = 12f, heightModWidth = 8f,
                } },
            };
            foreach (var tile in grid.Output.Tiles)
                for (int i = 0; i < tile.Heights.Length; i++) tile.Heights[i] = 0.02f;
            var ledger = new PlacementLedger();
            ledger.Add(new LedgerPlacement("baker-seam-tree", new Vector3(-config.terrainWidth / 32f, 0f, 0f),
                Vector3.one, TerrainSurroundEffectType.rockNoBareGround, null));
            _directory = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "vtg-baker-seams-" + Guid.NewGuid().ToString("N")));
            TerrainFileWriter.Write(_directory, grid.Output);
            var baker = CreateBaker(config, ledger);
            var tiles = new float[4][,];

            // 非所有タイルから焼き、未要求の所有者を高さファイルから解決させる
            // Bake nonowners first so unrequested owners must resolve from height files
            for (int i = 3; i >= 0; i--) tiles[i] = baker.Bake(i % 2, i / 2).DisplayHeights;
            for (int k = 0; k < 17; k++)
            {
                Assert.That(tiles[0][k, 16], Is.EqualTo(tiles[1][k, 0]));
                Assert.That(tiles[0][16, k], Is.EqualTo(tiles[2][0, k]));
                Assert.That(tiles[2][k, 16], Is.EqualTo(tiles[3][k, 0]));
                Assert.That(tiles[1][16, k], Is.EqualTo(tiles[3][0, k]));
            }

            // 一枚だけcacheを取り逃し、残りのhitと同じ厳密な表示へ戻る
            // Miss just one cache entry and reconstruct the same exact display beside hits
            string path = _directory.TerrainVisualCacheFilePath(1, 1);
            Assert.That(File.Exists(path), Is.True);
            File.Delete(path);
            var reloaded = CreateBaker(config, ledger);
            for (int i = 0; i < 4; i++)
                CollectionAssert.AreEqual(tiles[i], reloaded.Bake(i % 2, i / 2).DisplayHeights);
            Assert.That(File.Exists(path), Is.True);
        }

        private TileVisualBaker CreateBaker(TerrainGenerationConfig config, PlacementLedger ledger)
        {
            var sections = new BiomeVisualSections(new[] { "fixture/grass" },
                new[] { new BiomeTextureConfig { entries = Array.Empty<TextureEntry>() } },
                new[] { new BiomeDetailConfig { entries = Array.Empty<DetailEntry>() } },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(Biomes.Length));
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Biomes);
            var layers = SplatLayerTable.Build("fixture/beach", "fixture/rock", sections.MainLayerAddresses,
                sections.TextureConfigs, sections.SurroundTextureConfigs, species, Array.Empty<string>());
            var origins = MapGenerationPipeline.ResolveOrigins(config);
            var version = WorldGeneratorVersion.Current + MapGenerationAlgorithmTable.ResolveSurface(config.surfaceRevision).VisualCacheVersionSuffix;
            var key = TerrainVisualCacheKey.Compute(new string('a', 64), config.seed, origins, config.Resolution,
                version, ledger.ComputeDigest());
            return new TileVisualBaker(config, Biomes, sections, layers, species,
                new MaterializedPlacementLedgerSource(ledger), ledger.ComputeDigest(), _directory,
                new TerrainVisualCache(_directory, key));
        }
    }
}
