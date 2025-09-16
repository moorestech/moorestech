using System;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.EventProtocol;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ResearchCompleteEventPacketTest
    {
        [Test]
        public void ResearchCompleteToEventPacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // イベントがないことを確認する
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);

            // Research 1を完了させる
            CompleteResearchForTest(serviceProvider, Research1Guid);

            // イベントを受け取り、テストする
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());

            var researchEvents = eventMessagePack.Events
                .Where(e => e.Tag == ResearchCompleteEventPacket.EventTag)
                .ToList();

            // 研究完了イベントが1件であることを確認
            Assert.AreEqual(1, researchEvents.Count);

            // 研究完了イベントを確認
            VerifyResearchCompleteEvent(researchEvents[0], Research1Guid);

            // Research 2を完了させる（前提条件付き）
            CompleteResearchForTest(serviceProvider, Research2Guid);

            // イベントを受け取り、テストする
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());

            researchEvents = eventMessagePack.Events
                .Where(e => e.Tag == ResearchCompleteEventPacket.EventTag)
                .ToList();

            // ResearchCompleteイベントが1件であることを確認
            Assert.AreEqual(1, researchEvents.Count);

            // Research 2の完了イベントを確認
            VerifyResearchCompleteEvent(researchEvents[0], Research2Guid);
        }

        private void VerifyResearchCompleteEvent(EventMessagePack eventData, Guid expectedResearchGuid)
        {
            Assert.AreEqual(ResearchCompleteEventPacket.EventTag, eventData.Tag);

            var researchCompleteData = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(eventData.Payload);
            Assert.AreEqual(PlayerId, researchCompleteData.PlayerId);
            Assert.AreEqual(expectedResearchGuid.ToString(), researchCompleteData.ResearchGuidStr);
        }
    }
}
