using NUnit.Framework;
using Server;

namespace Test.CombinedTest.Server.PacketTest
{
    public class SendCommandProtocolTest
    {
        [Test]
        public void GiveCommandTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
        }
    }
}