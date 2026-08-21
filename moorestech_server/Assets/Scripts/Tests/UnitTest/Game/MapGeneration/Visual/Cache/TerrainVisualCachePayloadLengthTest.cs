using Game.MapGeneration.Cache;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    public class TerrainVisualCachePayloadLengthTest
    {
        [Test]
        public void AcceptsTheCurrentProductionVisualPayloadLength()
        {
            Assert.That(TerrainVisualCacheFormat.TryCalculatePayloadByteLength(2048, 19, 2048, 24, out var payloadByteLength), Is.True);
            Assert.That(payloadByteLength, Is.EqualTo(281018368L));
        }
    }
}
