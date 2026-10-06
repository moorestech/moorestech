using System;
using Game.MapGeneration.Surface;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;
using Game.MapGeneration.Pipeline;
using Tests.UnitTest.Game.MapGeneration.Surface;

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    public abstract class TileVisualBakerGateFixture
    {
        protected const int Resolution = 33;
        protected const int AlphamapResolution = Resolution - 1;
        protected const int DetailResolution = 16;
        protected const float TileSize = 100f;
        protected const int TileX = 0;
        protected const int TileZ = 0;

        // 実物と同じ64文字の16進。長さが違うと書き込み時点で弾かれる
        // The real 64-hex-character shape; a different length is rejected at write time
        protected const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        protected static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };
        protected static readonly PlacementLedger EmptyLedger = new();

        protected WorldDataDirectory _worldCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_tile_visual_gate_{Guid.NewGuid()}");
            _worldCacheDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            Directory.CreateDirectory(_worldCacheDirectory.TerrainDirectory);

            // 高さは全画素0でよい。木の摂動もHeightFileLoaderのr16読み出し長も、平坦な高さ配列で足りる
            // Flat zero heights suffice: neither the tree perturbation nor HeightFileLoader's r16 read length needs anything richer
            File.WriteAllBytes(_worldCacheDirectory.TerrainHeightFilePath(TileX, TileZ), new byte[Resolution * Resolution * 2]);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldCacheDirectory.Root)) Directory.Delete(_worldCacheDirectory.Root, true);
        }

        // 全画素を0xFFFFで埋める。正規化高さ1.0はゲート判定と区別できる非平坦値になる
        // Fills every pixel with 0xFFFF; normalized height 1.0 is a non-flat value distinguishable from the gate's off-state
        protected void WriteMaxHeightFile()
        {
            var bytes = new byte[Resolution * Resolution * 2];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = 0xFF;
            File.WriteAllBytes(_worldCacheDirectory.TerrainHeightFilePath(TileX, TileZ), bytes);
        }

        protected TileVisualBaker CreateBaker(bool generateTexture, bool generateDetail, bool generateHeightmap)
        {
            return CreateBaker(generateTexture, generateDetail, generateHeightmap,
                new MaterializedGenerationRunSource(LedgerRunFixture.Legacy(EmptyLedger)), EmptyLedger.ComputeDigest());
        }

        protected TileVisualBaker CreateBaker(
            bool generateTexture, bool generateDetail, bool generateHeightmap,
            IGenerationRunSource runSource, string expectedPlacementLedgerDigest)
        {
            var config = CreateConfig(generateTexture, generateDetail, generateHeightmap);
            var visualSections = CreateVisualSections();
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), BiomeTypes);
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, Array.Empty<string>());

            return new TileVisualBaker(
                config, BiomeTypes, visualSections, layerTable, treeSurroundSpecies, runSource,
                expectedPlacementLedgerDigest, _worldCacheDirectory, new TerrainVisualCache(_worldCacheDirectory, CacheKey));
        }

        private static TerrainGenerationConfig CreateConfig(bool generateTexture, bool generateDetail, bool generateHeightmap)
        {
            return new TerrainGenerationConfig
            {
                surfaceRevision = WorldSurfaceRevision.Legacy4,
                overrideResolution = Resolution,
                detailResolution = DetailResolution,
                seed = 12345,
                terrainWidth = TileSize,
                terrainLength = TileSize,
                terrainHeight = 600f,
                generateTexture = generateTexture,
                generateDetail = generateDetail,
                generateHeightmap = generateHeightmap,
                grasslandEnabled = true,
                forestEnabled = false,
                savannaEnabled = false,
                desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = false,
                jungleEnabled = false,
                woodsEnabled = false,
            };
        }

        // detailエントリは1本だけ。プロトタイプと密度マップの数が食い違えばそのまま1対0として現れる
        // A single detail entry, so any divergence between prototypes and density maps shows up plainly as one against zero
        private static BiomeVisualSections CreateVisualSections()
        {
            return new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = Array.Empty<TextureEntry>() } },
                new[]
                {
                    new BiomeDetailConfig
                    {
                        entries = new[] { DetailTestConfigBuilder.CreateEntry(1f, 8) },
                        filterRejectThreshold = 0.01f,
                        borderMargin = 0f,
                    },
                },
                DetailTestConfigBuilder.CreateDisabledSurroundConfigs(BiomeTypes.Length));
        }

        protected sealed class CountingGenerationRunSource : IGenerationRunSource
        {
            private readonly GenerationRun _run;
            public int ResolveCount { get; private set; }

            public CountingGenerationRunSource(PlacementLedger ledger)
            {
                _run = LedgerRunFixture.Legacy(ledger);
            }

            public GenerationRun Resolve()
            {
                ResolveCount++;
                return _run;
            }
        }
    }
}
