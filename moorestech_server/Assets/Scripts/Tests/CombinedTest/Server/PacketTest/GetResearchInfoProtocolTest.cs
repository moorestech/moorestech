using System;
using System.Linq;
using Game.Research;
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
        public void GetResearchNodeStatesAreReturned()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var emptyResponse = SendGetResearchInfoRequest(packet);
            Assert.IsNotNull(emptyResponse);
            Assert.AreEqual(4, emptyResponse.ResearchNodeStates.Count);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, GetNodeState(emptyResponse, Research1Guid));
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, GetNodeState(emptyResponse, Research2Guid));
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, GetNodeState(emptyResponse, Research3Guid));
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, GetNodeState(emptyResponse, Research4Guid));

            CompleteResearchForTest(serviceProvider, Research1Guid);
            CompleteResearchForTest(serviceProvider, Research2Guid);

            var response = SendGetResearchInfoRequest(packet);

            Assert.AreEqual(ResearchNodeState.Completed, GetNodeState(response, Research1Guid));
            Assert.AreEqual(ResearchNodeState.Completed, GetNodeState(response, Research2Guid));
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, GetNodeState(response, Research3Guid));
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, GetNodeState(response, Research4Guid));
        }

        private GetResearchInfoProtocol.ResponseResearchInfoMessagePack SendGetResearchInfoRequest(PacketResponseCreator packet)
        {
            var request = new GetResearchInfoProtocol.RequestResearchInfoMessagePack(PlayerId);
            var requestData = MessagePackSerializer.Serialize(request).ToList();
            var response = packet.GetPacketResponse(requestData);

            return MessagePackSerializer.Deserialize<GetResearchInfoProtocol.ResponseResearchInfoMessagePack>(response[0].ToArray());
        }

        private ResearchNodeState GetNodeState(GetResearchInfoProtocol.ResponseResearchInfoMessagePack response, Guid researchGuid)
        {
            return response.ResearchNodeStates
                .First(s => s.ResearchGuid == researchGuid)
                .ResearchNodeState;
        }
    }
}
