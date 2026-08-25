using Game.MapGeneration.Cache;
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
    }
}
