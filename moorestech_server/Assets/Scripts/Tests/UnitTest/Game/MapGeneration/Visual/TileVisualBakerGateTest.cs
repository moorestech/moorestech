using System;
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

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    /// <summary>
    ///     generateTexture / generateDetail が見た目の再構築を切ることを検証する。detailはプロトタイプと密度マップが
    ///     必ず同数でなければならず、片方だけ止めた実装はGeneratedTerrainSourceの数一致検査で落ちる
    ///     Verifies generateTexture and generateDetail gate the visual rebuild; detail prototypes and density maps must
    ///     always match in count, and gating only one of them trips GeneratedTerrainSource's count check
    /// </summary>
    public class TileVisualBakerGateTest
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
        private static readonly PlacementLedger EmptyLedger = new();

        private WorldDataDirectory _worldCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_tile_visual_gate_{Guid.NewGuid()}");
            _worldCacheDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            Directory.CreateDirectory(_worldCacheDirectory.TerrainDirectory);

            // 転送済みバイオームはsplat経路だけが読む。テクスチャONの側を実際に走らせるために置く
            // The transferred biomes are read by the splat path alone and exist so the texture-on side actually runs
            var transferredBiomeIndices = new byte[Resolution * Resolution];
            for (var pixel = 0; pixel < transferredBiomeIndices.Length; pixel++)
                transferredBiomeIndices[pixel] = (byte)BiomeType.Grassland;
            File.WriteAllBytes(_worldCacheDirectory.TerrainBiomeFilePath(TileX, TileZ), transferredBiomeIndices);

            // 高さは全画素0でよい。木の摂動もHeightFileLoaderのr16読み出し長も、平坦な高さ配列で足りる
            // Flat zero heights suffice: neither the tree perturbation nor HeightFileLoader's r16 read length needs anything richer
            File.WriteAllBytes(_worldCacheDirectory.TerrainHeightFilePath(TileX, TileZ), new byte[Resolution * Resolution * 2]);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldCacheDirectory.Root)) Directory.Delete(_worldCacheDirectory.Root, true);
        }

        [Test]
        public void BuildsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOn()
        {
            var baker = CreateBaker(true, true);
            var baked = baker.Bake(TileX, TileZ);

            Assert.That(baked.DetailMaps.Count, Is.EqualTo(baker.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(baker.DetailPrototypes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DropsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOff()
        {
            var baker = CreateBaker(true, false);
            var baked = baker.Bake(TileX, TileZ);

            // 片方だけ止めるとGeneratedTerrainSourceの数一致検査で落ちる。同数であることが本体の要求
            // Gating only one side trips GeneratedTerrainSource's count check; matching counts are what production demands
            Assert.That(baked.DetailMaps.Count, Is.EqualTo(baker.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(baker.DetailPrototypes.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuildsTheAlphamapWhenTextureGenerationIsOn()
        {
            var baked = CreateBaker(true, true).Bake(TileX, TileZ);

            Assert.That(baked.Alphamap, Is.Not.Null);
            Assert.That(baked.Alphamap.GetLength(0), Is.EqualTo(AlphamapResolution));
        }

        [Test]
        public void LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff()
        {
            var baked = CreateBaker(false, true).Bake(TileX, TileZ);

            // alphamapが無いことがSplatmapRuntimeGenerateを通っていない唯一の観測点
            // The absent alphamap is the single observable telling SplatmapRuntimeGenerator never ran
            Assert.That(baked.Alphamap, Is.Null);
            Assert.That(baked.DetailMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReusesTheCachedVisualOnASecondBakeWhenTextureGenerationIsOn()
        {
            var baker = CreateBaker(true, true);
            baker.Bake(TileX, TileZ);

            var cacheFilePath = _worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ);
            Assert.That(File.Exists(cacheFilePath), Is.True);
            var writeTimeAfterFirstBake = File.GetLastWriteTimeUtc(cacheFilePath);

            baker.Bake(TileX, TileZ);

            // ヒットは書き戻さない。更新時刻が動いていないことがヒットの証拠になる
            // A hit never writes back, so an unmoved timestamp is the evidence of the hit
            Assert.That(File.GetLastWriteTimeUtc(cacheFilePath), Is.EqualTo(writeTimeAfterFirstBake));
        }

        [Test]
        public void NeitherReadsNorWritesTheCacheWhenTextureGenerationIsOff()
        {
            // キャッシュ形式はalphamapを必ず1枚要求する。テクスチャ無しの見た目は書き出せない
            // The cache format always demands one alphamap, so a texture-less visual cannot be written at all
            var baker = CreateBaker(false, true);
            baker.Bake(TileX, TileZ);
            baker.Bake(TileX, TileZ);

            Assert.That(File.Exists(_worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ)), Is.False);
        }

        private TileVisualBaker CreateBaker(bool generateTexture, bool generateDetail)
        {
            var config = CreateConfig(generateTexture, generateDetail);
            var visualSections = CreateVisualSections();
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), BiomeTypes);
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, Array.Empty<string>());

            return new TileVisualBaker(
                config, BiomeTypes, visualSections, layerTable, treeSurroundSpecies, EmptyLedger,
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
