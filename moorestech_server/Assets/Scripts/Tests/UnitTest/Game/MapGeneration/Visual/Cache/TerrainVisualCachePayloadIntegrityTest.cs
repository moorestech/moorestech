using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual;
using Game.Paths;
using NUnit.Framework;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     同じ長さのpayload破損を取り逃しにすることを検証する
    ///     Verifies same-length payload corruption becomes a cache miss
    /// </summary>
    public class TerrainVisualCachePayloadIntegrityTest
    {
        private const int HeightmapResolution = 17;
        private const int AlphamapResolution = 4;
        private const int LayerCount = 3;
        private const int DetailResolution = 16;
        private const int DetailMapCount = 2;
        private const int TileX = 1;
        private const int TileZ = 2;
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private string _worldRootDirectory;

        [SetUp]
        public void SetUp()
        {
            _worldRootDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_visual_cache_integrity_{Guid.NewGuid()}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldRootDirectory)) Directory.Delete(_worldRootDirectory, true);
        }

        [Test]
        public void MissesWhenOneHeightPayloadBitChangesWithoutChangingLength()
        {
            var cache = CreateCache();
            cache.Save(TileX, TileZ, CreateTileVisual());

            // 表示用高さもキャッシュが持つ区画なので、ここが化けると地面の形だけが静かに壊れる
            // The display heights are a cached section too, so corruption here quietly breaks the ground shape alone
            FlipPayloadBit(HeaderByteLength);

            Assert.That(TryLoad(cache), Is.False);
        }

        [Test]
        public void MissesWhenOneAlphamapPayloadBitChangesWithoutChangingLength()
        {
            var cache = CreateCache();
            cache.Save(TileX, TileZ, CreateTileVisual());

            FlipPayloadBit(HeaderByteLength + (int)HeightsByteLength(HeightmapResolution));

            Assert.That(TryLoad(cache), Is.False);
        }

        [Test]
        public void MissesWhenOneDetailPayloadBitChangesWithoutChangingLength()
        {
            var cache = CreateCache();
            cache.Save(TileX, TileZ, CreateTileVisual());

            FlipPayloadBit(HeaderByteLength + (int)HeightsByteLength(HeightmapResolution) +
                           (int)(AlphamapPlaneCount(LayerCount) * AlphamapPlaneByteLength(AlphamapResolution)));

            Assert.That(TryLoad(cache), Is.False);
        }

        private static bool TryLoad(TerrainVisualCache cache)
        {
            return cache.TryLoad(TileX, TileZ, HeightmapResolution, AlphamapResolution, LayerCount, DetailResolution,
                DetailMapCount, out _);
        }

        private TerrainVisualCache CreateCache()
        {
            return new TerrainVisualCache(WorldDataDirectory.FromWorldRoot(_worldRootDirectory), CacheKey);
        }

        private void FlipPayloadBit(int offset)
        {
            var filePath = WorldDataDirectory.FromWorldRoot(_worldRootDirectory).TerrainVisualCacheFilePath(TileX, TileZ);
            var bytes = File.ReadAllBytes(filePath);
            bytes[offset] ^= 0x01;
            File.WriteAllBytes(filePath, bytes);
        }

        private static TerrainTileVisual CreateTileVisual()
        {
            var displayHeights = new float[HeightmapResolution, HeightmapResolution];
            var alphamap = new float[AlphamapResolution, AlphamapResolution, LayerCount];
            var detailMaps = new List<int[,]>(DetailMapCount);
            for (var mapIndex = 0; mapIndex < DetailMapCount; mapIndex++)
                detailMaps.Add(new int[DetailResolution, DetailResolution]);

            return new TerrainTileVisual(
                displayHeights,
                TileAlphamap.Create(StoredAlphamapWeights.ToPlanes(alphamap), AlphamapResolution, LayerCount),
                detailMaps);
        }
    }
}
