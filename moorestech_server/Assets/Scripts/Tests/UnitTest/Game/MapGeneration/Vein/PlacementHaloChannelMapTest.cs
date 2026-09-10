using Game.MapGeneration.Pipeline.Tiling;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    public class PlacementHaloChannelMapTest
    {
        [Test]
        public void SameEntryReturnsSameChannelAndDifferentEntryReturnsDifferentChannel()
        {
            var map = new PlacementHaloChannelMap();

            var channelA1 = map.GetOrCreate(0);
            var channelA2 = map.GetOrCreate(0);
            var channelB = map.GetOrCreate(1);

            Assert.That(channelA2, Is.SameAs(channelA1));
            Assert.That(channelB, Is.Not.SameAs(channelA1));
        }
    }
}
