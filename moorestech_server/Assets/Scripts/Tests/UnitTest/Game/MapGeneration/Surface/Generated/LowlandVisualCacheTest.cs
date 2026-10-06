using System;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Export;
using Game.MapGeneration.Surface;
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
using Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public class LowlandVisualCacheTest
    {
        private WorldDataDirectory _directory;
        private static readonly BiomeType[] Biomes = { BiomeType.Grassland };

        [TearDown]
        public void TearDown()
        {
            if (_directory != null && Directory.Exists(_directory.Root)) Directory.Delete(_directory.Root, true);
        }

        [Test]
        [Timeout(1500000)]
        public void NegativeTreeOnBelowSeaLandIsProtectedAcrossBakerMissHitAndReload()
        {
            var config = CreateConfig();
            var ledger = new PlacementLedger();
            ledger.Add(new LedgerPlacement("negative-tree", new Vector3(16f, 3f, 16f), Vector3.one,
                TerrainSurroundEffectType.rockNoBareGround, null));
            var output = new MapGenerationOutput { Resolution = 33 };
            var flat = new float[33 * 33];
            for (int index = 0; index < flat.Length; index++) flat[index] = 3f / config.terrainHeight;
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = flat });
            _directory = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "vtg-low-cache-" + Guid.NewGuid().ToString("N")));
            TerrainFileWriter.Write(_directory, output);

            // 全域が陸で木の変位が負であること
            // Verify all land and that trees depress it
            var classified = SurfaceGridBuilder.Build(config);
            Assert.That(classified.Land.ContainsSupport(new Rect(0f, 0f, 32f, 32f)), Is.True);
            var pre = HeightFileLoader.LoadHeights(_directory, 0, 0, 33);
            var depressed = TreePerturbationApplier.Apply(pre, config, Vector3.zero, ledger.Placements);
            Assert.That(depressed[16, 16] * config.terrainHeight, Is.LessThan(0f));

            var first = CreateBaker(config, ledger, classified.Land).Bake(0, 0);
            var path = _directory.TerrainVisualCacheFilePath(0, 0);
            Assert.That(File.Exists(path), Is.True, "A miss must actually write the visual cache");
            var writeTime = File.GetLastWriteTimeUtc(path);
            var hit = CreateBaker(config, ledger, classified.Land).Bake(0, 0);
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(writeTime), "A hit must not rewrite the cache");

            // cache除去後もbakerで低地復元
            // Rebuild from saved lowland with the production baker after cache removal
            File.Delete(path);
            var miss = CreateBaker(config, ledger, classified.Land).Bake(0, 0);
            Assert.That(File.Exists(path), Is.True);
            SurfaceHeightAssert.AreEqual(first.DisplayHeights, hit.DisplayHeights, "lowland reload-hit");
            SurfaceHeightAssert.AreEqual(first.DisplayHeights, miss.DisplayHeights, "lowland reload-miss");
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
                Assert.That(miss.DisplayHeights[z, x] * config.terrainHeight, Is.GreaterThanOrEqualTo(SurfaceGuaranteeBounds.LandMinimum));
            Assert.That(HeightFileLoader.LoadHeights(_directory, 0, 0, 33)[16, 16], Is.EqualTo(pre[16, 16]));
            TestContext.WriteLine("Synthetic negative-tree/cache regression; no reproduction of the sea exposure report is claimed.");
        }

        private TileVisualBaker CreateBaker(TerrainGenerationConfig config, PlacementLedger ledger, LandCellField land)
        {
            var sections = new BiomeVisualSections(new[] { "fixture/grass" },
                new[] { new BiomeTextureConfig { entries = Array.Empty<TextureEntry>() } },
                new[] { new BiomeDetailConfig { entries = Array.Empty<DetailEntry>() } },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(Biomes.Length));
            var species = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), Biomes);
            var layers = SplatLayerTable.Build("fixture/beach", "fixture/rock", sections.MainLayerAddresses,
                sections.TextureConfigs, sections.SurroundTextureConfigs, species, Array.Empty<string>());

            // 合成鍵でも本番revisionと台帳指紋
            // Use production revision and ledger digest even for synthetic keys
            var origins = MapGenerationPipeline.ResolveOrigins(config);
            var key = TerrainVisualCacheKey.Compute(new string('a', 64), config.seed, origins, config.Resolution,
                WorldGeneratorVersion.Current, ledger.ComputeDigest());
            return new TileVisualBaker(config, Biomes, sections, layers, species,
                new MaterializedPlacementLedgerSource(LedgerRunFixture.Grounded(ledger, land)), ledger.ComputeDigest(), _directory,
                new TerrainVisualCache(_directory, key));
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            var config = new TerrainGenerationConfig
            {
                surfaceRevision = WorldSurfaceRevision.Grounded5,
                overrideResolution = 33,
                terrainWidth = 32f,
                terrainLength = 32f,
                gridSizeX = 1,
                gridSizeZ = 1,
                landThreshold = -1f,
                detailResolution = 16,
                generateDetail = false,
                forestEnabled = false,
                savannaEnabled = false,
                desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = false,
                jungleEnabled = false,
                woodsEnabled = false,
            };
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    new TreePrototypeEntry
                    {
                        mapObjectGuids = new[] { "negative-tree" },
                        heightModAmount = -12f,
                        heightModWidth = 4f,
                    },
                },
            };
            return config;
        }
    }
}
