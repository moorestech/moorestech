using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Transfer;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    public class TerrainVisualCachePayloadLengthTest
    {
        [Test]
        public void AcceptsTheCurrentProductionVisualPayloadLength()
        {
            Assert.That(TerrainVisualCacheFormat.TryCalculatePayloadByteLength(2049, 2048, 19, 2048, 24, out var payloadByteLength), Is.True);
            Assert.That(payloadByteLength, Is.EqualTo(8396802L + 5L * 2048 * 2048 * 4 + 24L * 2048 * 2048 * 2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(48)]
        public void RejectsDetailDimensionsOutsideTheCacheContract(int detailResolution)
        {
            Assert.That(TerrainVisualCacheFormat.TryCalculatePayloadByteLength(
                33, 32, 3, detailResolution, 1, out _), Is.False);
        }

        // 読み手が拒む寸法を書き手が受けると、書けるが読めないキャッシュが無警告で溜まる
        // A writer accepting dimensions the reader rejects would silently pile up caches that can be written but never read
        [Test]
        public void TheWriterRefusesDimensionsTheReaderWouldReject()
        {
            const int heightmapResolution = 33;
            const int alphamapResolution = 32;
            const int layerCount = 3;
            const int invalidDetailResolution = 17;

            var planes = new byte[TileAlphamap.AlphamapPlaneCount(layerCount)][];
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                planes[planeIndex] = new byte[alphamapResolution * alphamapResolution * TileAlphamap.AlphamapPlaneBytesPerPixel];

            var tileVisual = new TerrainTileVisual(
                new float[heightmapResolution, heightmapResolution],
                TileAlphamap.CreateOwning(planes, alphamapResolution, layerCount),
                new List<int[,]> { new int[invalidDetailResolution, invalidDetailResolution] });

            var filePath = Path.Combine(Path.GetTempPath(), "TerrainVisualCacheWriterTest_" + Guid.NewGuid().ToString("N") + ".bin");
            Assert.Throws<InvalidOperationException>(
                () => TerrainVisualCacheWriter.Write(filePath, new string('a', TerrainVisualCacheFormat.CacheKeyByteLength), tileVisual));
            Assert.That(File.Exists(filePath), Is.False);
        }
    }
}
