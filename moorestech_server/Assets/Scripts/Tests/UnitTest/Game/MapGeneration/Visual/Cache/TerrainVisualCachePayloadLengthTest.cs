using Game.MapGeneration.Cache;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    public class TerrainVisualCachePayloadLengthTest
    {
        [Test]
        public void AcceptsTheCurrentProductionVisualPayloadLength()
        {
            // 実運用の寸法（高さ2049・alphamap2048×19層・detail2048×24枚）が32bit長に収まることを固定する
            // Pins that the production dimensions (2049 heights, 2048x19 alphamap, 24 detail maps at 2048) still fit a 32-bit length
            Assert.That(TerrainVisualCacheFormat.TryCalculatePayloadByteLength(2049, 2048, 19, 2048, 24, out var payloadByteLength), Is.True);
            Assert.That(payloadByteLength, Is.EqualTo(8396802L + 5L * 2048 * 2048 * 4 + 24L * 2048 * 2048 * 2));
        }
    }
}
