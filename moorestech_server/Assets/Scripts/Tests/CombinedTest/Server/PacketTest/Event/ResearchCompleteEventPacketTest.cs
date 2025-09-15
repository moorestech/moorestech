using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ResearchCompleteEventPacketTest
    { 
        [Test]
        public void ResearchCompleteToEventPacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

        }
    }
}
