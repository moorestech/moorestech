using System;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetResearchInfoProtocolTest
    {
        [Test]
        public void GetCompletedResearchGuidsAreReturned()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var emptyResponse = SendGetCompletedResearchRequest(packet);
            Assert.IsNotNull(emptyResponse);
            Assert.AreEqual(0, emptyResponse.CompletedResearchGuidStrings.Count);

            CompleteResearchForTest(serviceProvider, Research1Guid);
            CompleteResearchForTest(serviceProvider, Research2Guid);

            var response = SendGetCompletedResearchRequest(packet);

            Assert.That(response.CompletedResearchGuidStrings, Does.Contain(Research1Guid.ToString()));
            Assert.That(response.CompletedResearchGuidStrings, Does.Contain(Research2Guid.ToString()));
            Assert.IsFalse(response.CompletedResearchGuidStrings.Contains(Research3Guid.ToString()));
        }

        private GetResearchInfoProtocol.ResponseResearchInfoMessagePack SendGetCompletedResearchRequest(PacketResponseCreator packet)
        {
            var requestData = MessagePackSerializer.Serialize(new GetResearchInfoProtocol.RequestResearchInfoMessagePack()).ToList();
            var response = packet.GetPacketResponse(requestData);

            return MessagePackSerializer.Deserialize<GetResearchInfoProtocol.ResponseResearchInfoMessagePack>(response[0].ToArray());
        }
    }
}
