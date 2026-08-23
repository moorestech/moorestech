using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    public class PlacementHaloChannelMapTest
    {
        [Test]
        public void SameGuidReturnsSameChannelAndDifferentGuidReturnsDifferentChannel()
        {
            var map = new PlacementHaloChannelMap();

            var channelA1 = map.Get("guid-a");
            var channelA2 = map.Get("guid-a");
            var channelB = map.Get("guid-b");

            Assert.That(channelA2, Is.SameAs(channelA1));
            Assert.That(channelB, Is.Not.SameAs(channelA1));
        }
    }
}
