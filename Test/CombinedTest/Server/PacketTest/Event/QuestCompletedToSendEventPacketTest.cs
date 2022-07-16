using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class QuestCompletedToSendEventPacketTest
    {
        [Test]
        public void ItemCraftQuestCompletedToSendEventPacketTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
        }
    }
}