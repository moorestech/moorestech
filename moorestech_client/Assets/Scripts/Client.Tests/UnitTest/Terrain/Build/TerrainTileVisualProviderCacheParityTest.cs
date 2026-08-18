using System;
using System.IO;
using Client.Game.InGame.Environment.Terrain.Build;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Build
{
    /// <summary>
    ///     同じタイルを初回生成した見た目と、キャッシュから読み戻した見た目が一致することを検証する。
    ///     生成直後の重みは連続値なので、保存前に量子化まで畳んでおかないと読み戻しの側だけ値がずれ、
    ///     同じワールドでも再起動のたびに地面の色配分が変わる
    ///     Verifies the visuals a tile is first built with match the ones read back from the cache. Freshly generated
    ///     weights are continuous, so without folding them to the stored quantization before saving only the reloaded
    ///     side drifts, changing the same world's ground colours on every restart
    /// </summary>
    public class TerrainTileVisualProviderCacheParityTest
    {
        private const int Resolution = 9;
        private const int AlphamapResolution = Resolution - 1;
        private const float TileSize = 100f;
        private const int TileX = 0;
        private const int TileZ = 0;
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static readonly BiomeType[] BiomeTypes = { BiomeType.Grassland };
        private static readonly MapObjectLayoutMessagePack[] NoMapObjects = new MapObjectLayoutMessagePack[0];

        private TerrainGenerationConfig _tileConfig;
        private WorldDataDirectory _worldCacheDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), $"moorestech_tile_visual_parity_{Guid.NewGuid()}");
            _worldCacheDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);

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
        public void TheCachedAlphamapEqualsTheOneTheTileWasFirstBuiltWith()
        {
            var provider = CreateProvider();

            var first = Resolve(provider);
            var second = Resolve(provider);

            Assert.That(first.CacheHit, Is.False);
            Assert.That(second.CacheHit, Is.True, "2回目がキャッシュを引かないと往復を検証できない");
            Assert.That(HasFractionalWeight(first.Visual.Alphamap), Is.True, "0か1しかない盤面では量子化の有無が現れない");

            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            for (var layer = 0; layer < first.Visual.Alphamap.GetLength(2); layer++)
                Assert.That(
                    second.Visual.Alphamap[z, x, layer], Is.EqualTo(first.Visual.Alphamap[z, x, layer]),
                    $"z={z} x={x} layer={layer}");
        }

        private static bool HasFractionalWeight(float[,,] alphamap)
        {
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            for (var layer = 0; layer < alphamap.GetLength(2); layer++)
            {
                var weight = alphamap[z, x, layer];
                if (0f < weight && weight < 1f) return true;
            }

            return false;
        }

        private (TerrainTileVisual Visual, bool CacheHit) Resolve(TerrainTileVisualProvider provider)
        {
            var heights = new float[Resolution, Resolution];
            return provider.Resolve(TileX, TileZ, _tileConfig, Vector3.zero, heights, heights);
        }

        private TerrainTileVisualProvider CreateProvider()
        {
            var config = CreateConfig();
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

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
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
