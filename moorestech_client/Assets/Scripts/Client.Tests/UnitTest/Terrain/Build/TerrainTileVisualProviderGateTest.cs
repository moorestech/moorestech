using System;
using System.IO;
using Client.Game.InGame.Environment.Terrain.Build;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Build
{
    /// <summary>
    ///     generateTexture / generateDetail が見た目の再構築を切ることを検証する。detailはプロトタイプと密度マップが
    ///     必ず同数でなければならず、片方だけ止めた実装はGeneratedTerrainSourceの数一致検査で落ちる
    ///     Verifies generateTexture and generateDetail gate the visual rebuild; detail prototypes and density maps must
    ///     always match in count, and gating only one of them trips GeneratedTerrainSource's count check
    /// </summary>
    public class TerrainTileVisualProviderGateTest
    {
        private const int Resolution = 9;
        private const int AlphamapResolution = Resolution - 1;
        private const float TileSize = 100f;
        private const int TileX = 0;
        private const int TileZ = 0;

        // 実物と同じ64文字の16進。長さが違うと書き込み時点で弾かれる
        // The real 64-hex-character shape; a different length is rejected at write time
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };
        private static readonly LedgerPlacement[] NoMapObjects = new LedgerPlacement[0];

        private TerrainGenerationConfig _tileConfig;
        private WorldDataDirectory _worldCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_tile_visual_gate_{Guid.NewGuid()}");
            _worldCacheDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);

            // 転送済みバイオームはsplat経路だけが読む。テクスチャONの側を実際に走らせるために置く
            // The transferred biomes are read by the splat path alone and exist so the texture-on side actually runs
            Directory.CreateDirectory(_worldCacheDirectory.TerrainDirectory);
            var transferredBiomeIndices = new byte[Resolution * Resolution];
            for (var pixel = 0; pixel < transferredBiomeIndices.Length; pixel++)
                transferredBiomeIndices[pixel] = (byte)BiomeType.Grassland;

            File.WriteAllBytes(_worldCacheDirectory.TerrainBiomeFilePath(TileX, TileZ), transferredBiomeIndices);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldCacheDirectory.Root)) Directory.Delete(_worldCacheDirectory.Root, true);
        }

        [Test]
        public void BuildsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOn()
        {
            var provider = CreateProvider(true, true);
            var (visual, _) = Resolve(provider);

            Assert.That(visual.DetailMaps.Count, Is.EqualTo(provider.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(provider.DetailPrototypes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DropsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOff()
        {
            var provider = CreateProvider(true, false);
            var (visual, _) = Resolve(provider);

            // 片方だけ止めるとGeneratedTerrainSourceの数一致検査で落ちる。同数であることが本体の要求
            // Gating only one side trips GeneratedTerrainSource's count check; matching counts are what production demands
            Assert.That(visual.DetailMaps.Count, Is.EqualTo(provider.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(provider.DetailPrototypes.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuildsTheAlphamapWhenTextureGenerationIsOn()
        {
            var (visual, _) = Resolve(CreateProvider(true, true));

            Assert.That(visual.Alphamap, Is.Not.Null);
            Assert.That(visual.Alphamap.GetLength(0), Is.EqualTo(AlphamapResolution));
        }

        [Test]
        public void LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff()
        {
            var (visual, _) = Resolve(CreateProvider(false, true));

            // alphamapが無いことがSplatmapRuntimeGenerateを通っていない唯一の観測点
            // The absent alphamap is the single observable telling SplatmapRuntimeGenerator never ran
            Assert.That(visual.Alphamap, Is.Null);
            Assert.That(visual.DetailMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReusesTheCachedVisualOnASecondResolveWhenTextureGenerationIsOn()
        {
            var provider = CreateProvider(true, true);
            Resolve(provider);

            Assert.That(File.Exists(_worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ)), Is.True);
            Assert.That(Resolve(provider).CacheHit, Is.True);
        }

        [Test]
        public void NeitherReadsNorWritesTheCacheWhenTextureGenerationIsOff()
        {
            // キャッシュ形式はalphamapを必ず1枚要求する。テクスチャ無しの見た目は書き出せない
            // The cache format always demands one alphamap, so a texture-less visual cannot be written at all
            var provider = CreateProvider(false, true);
            Resolve(provider);

            Assert.That(File.Exists(_worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ)), Is.False);
            Assert.That(Resolve(provider).CacheHit, Is.False);
        }

        private (TerrainTileVisual Visual, bool CacheHit) Resolve(TerrainTileVisualProvider provider)
        {
            var heights = new float[Resolution, Resolution];
            return provider.Resolve(TileX, TileZ, _tileConfig, Vector3.zero, heights, heights);
        }

        private TerrainTileVisualProvider CreateProvider(bool generateTexture, bool generateDetail)
        {
            // 本番のtileConfigはconfigのShallowCopyでフラグが同一。テストでも同じ1本を両方へ渡す
            // In production the tile config is a shallow copy carrying the same flags, so one config feeds both here too
            var config = CreateConfig(generateTexture, generateDetail);
            _tileConfig = config;
            var visualSections = CreateVisualSections();
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), BiomeTypes);
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, Array.Empty<string>());

            return new TerrainTileVisualProvider(
                config, BiomeTypes, visualSections, layerTable,
                new TerrainLayer[layerTable.OrderedLayerAddresses.Count], treeSurroundSpecies, NoMapObjects,
                _worldCacheDirectory, new TerrainVisualCache(_worldCacheDirectory, CacheKey));
        }

        private static TerrainGenerationConfig CreateConfig(bool generateTexture, bool generateDetail)
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                seed = 12345,
                terrainWidth = TileSize,
                terrainLength = TileSize,
                terrainHeight = 600f,
                generateTexture = generateTexture,
                generateDetail = generateDetail,
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
    }
}
