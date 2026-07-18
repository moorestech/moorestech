using System;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ResearchCompleteEventPacketTest
    {
        [Test]
        public void ResearchCompleteToEventPacketTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // イベントがないことを確認する
            // Verify no events are pending yet
            Assert.AreEqual(0, sink.TakeAll().Count);

            // Research 1を完了させる
            // Complete research 1
            CompleteResearchForTest(serviceProvider, Research1Guid);

            // イベントを受け取り、テストする
            // Take the events and verify them
            // Take the events and verify them
            var events = sink.TakeAll();

            var researchEvents = events
                .Where(e => e.Tag == ResearchCompleteEventPacket.EventTag)
                .ToList();

            // 研究完了イベントが1件であることを確認
            Assert.AreEqual(1, researchEvents.Count);

            // 研究完了イベントを確認
            VerifyResearchCompleteEvent(researchEvents[0], Research1Guid);

            // Research 2を完了させる（前提条件付き）
            // Complete research 2 (which has a prerequisite)
            CompleteResearchForTest(serviceProvider, Research2Guid);

            // イベントを受け取り、テストする
            events = sink.TakeAll();

            researchEvents = events
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
