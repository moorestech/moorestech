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

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    /// <summary>
    ///     同じタイルを初回生成した見た目と、キャッシュから読み戻した見た目が一致することを検証する。
    ///     生成直後の重みは連続値なので、保存前に量子化まで畳んでおかないと読み戻しの側だけ値がずれ、
    ///     同じワールドでも再起動のたびに地面の色配分が変わる
    ///     Verifies the visuals a tile is first built with match the ones read back from the cache. Freshly generated
    ///     weights are continuous, so without folding them to the stored quantization before saving only the reloaded
    ///     side drifts, changing the same world's ground colours on every restart
    /// </summary>
    public class TileVisualBakerCacheParityTest
    {
        private const int Resolution = 9;
        private const int AlphamapResolution = Resolution - 1;
        private const float TileSize = 100f;
        private const int TileX = 0;
        private const int TileZ = 0;
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };
        private static readonly PlacementLedger EmptyLedger = new();

        private WorldDataDirectory _worldCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_tile_visual_parity_{Guid.NewGuid()}");
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

        [Test]
        public void TheCachedAlphamapEqualsTheOneTheTileWasFirstBuiltWith()
        {
            var baker = CreateBaker();

            var first = baker.Bake(TileX, TileZ);
            var cacheFilePath = _worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ);
            Assert.That(File.Exists(cacheFilePath), Is.True, "初回ビルドでキャッシュが書かれていない");
            var writeTimeAfterFirstBake = File.GetLastWriteTimeUtc(cacheFilePath);

            var second = baker.Bake(TileX, TileZ);

            // ヒットは書き戻さない。更新時刻が動いていないことが2回目がキャッシュを引いた証拠になる
            // A hit never writes back, so an unmoved timestamp is the evidence the second bake read the cache
            Assert.That(File.GetLastWriteTimeUtc(cacheFilePath), Is.EqualTo(writeTimeAfterFirstBake), "2回目がキャッシュを引かないと往復を検証できない");
            Assert.That(HasFractionalWeight(first.AlphamapPlanes), Is.True, "0か1しかない盤面では量子化の有無が現れない");

            Assert.That(second.AlphamapPlanes.Count, Is.EqualTo(first.AlphamapPlanes.Count));
            for (var planeIndex = 0; planeIndex < first.AlphamapPlanes.Count; planeIndex++)
                Assert.That(second.AlphamapPlanes[planeIndex], Is.EqualTo(first.AlphamapPlanes[planeIndex]), $"plane={planeIndex}");

            // 表示用高さもキャッシュ往復の対象になったので、木の摂動ごと一致することを見る
            // The display heights became part of the round trip too, so the tree perturbation must survive it as well
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                Assert.That(second.DisplayHeights[z, x], Is.EqualTo(first.DisplayHeights[z, x]).Within(1f / ushort.MaxValue),
                    $"height z={z} x={x}");
        }

        private static bool HasFractionalWeight(System.Collections.Generic.IReadOnlyList<byte[]> alphamapPlanes)
        {
            foreach (var plane in alphamapPlanes)
            foreach (var weight in plane)
                if (0 < weight && weight < byte.MaxValue) return true;

            return false;
        }

        private TileVisualBaker CreateBaker()
        {
            var config = CreateConfig();
            var visualSections = CreateVisualSections();
            var treeSurroundSpecies = TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), BiomeTypes);
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, treeSurroundSpecies, Array.Empty<string>());

            return new TileVisualBaker(
                config, BiomeTypes, visualSections, layerTable, treeSurroundSpecies, new MaterializedPlacementLedgerSource(EmptyLedger),
                _worldCacheDirectory, new TerrainVisualCache(_worldCacheDirectory, CacheKey));
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                detailResolution = Resolution - 1,
                seed = 12345,
                terrainWidth = TileSize,
                terrainLength = TileSize,
                terrainHeight = 600f,
                generateTexture = true,
                generateDetail = true,
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

        // ノイズ変調するエントリを1本入れて重みを画素ごとの端数にする。全画素0/1の盤面では量子化の有無が現れない
        // One noise-modulated entry gives every pixel a fractional weight; on an all 0/1 board the quantization would be invisible
        private static BiomeVisualSections CreateVisualSections()
        {
            var noisyEntry = new TextureEntry
            {
                layerAddressablePath = "addr/rockface",
                weight = 1f,
                noiseType = MapNoiseType.Simple,
                noiseFrequency = 0.05f,
                noiseAmplitude = 1f,
            };

            return new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new[] { noisyEntry } } },
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
