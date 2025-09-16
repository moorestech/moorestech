using System;
using System.Linq;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.EventProtocol;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ResearchCompleteEventPacketTest
    {
        private ServiceProvider _serviceProvider;
        private PacketResponseCreator _packetResponse;

        [SetUp]
        public void SetUp()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packetResponse = packetResponse;
            
            // EventProtocolProviderのイベントをクリア
            var eventProtocolProvider = _serviceProvider.GetService<EventProtocolProvider>();
            eventProtocolProvider.ClearAllEvents();
        }

        [TearDown]
        public void TearDown()
        {
            var researchEvent = _serviceProvider.GetService<ResearchEvent>();
            researchEvent?.Clear();
            _serviceProvider?.Dispose();
        }

        [Test]
        public void ResearchCompleteToEventPacketTest()
        {
            // 既存のイベントをすべて消費してクリア
            var clearResponse = _packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var clearEventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(clearResponse[0].ToArray());
            var existingEventCount = clearEventMessagePack.Events.Count;

            // 既存イベントが存在する場合はログに記録（デバッグ用）
            if (existingEventCount > 0)
            {
                UnityEngine.Debug.Log($"Cleared {existingEventCount} existing events before test");
            }

            // イベントがクリアされたことを確認
            var response = _packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count, "Events should be cleared before test");

            // Research 1を完了させる
            CompleteResearchForTest(_serviceProvider, Research1Guid);

            // イベントを受け取り、テストする
            response = _packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());

            // 研究完了イベントを検証（他のイベントも含まれる可能性がある）
            var researchCompleteEvents = eventMessagePack.Events
                .Where(e => e.Tag == ResearchCompleteEventPacket.EventTag)
                .ToList();

            Assert.AreEqual(1, researchCompleteEvents.Count, "Should have exactly 1 research complete event after Research 1 completion");
            VerifyResearchCompleteEvent(researchCompleteEvents[0], Research1Guid);

            // Research 2を完了させる（前提条件付き）
            CompleteResearchForTest(_serviceProvider, Research2Guid);

            // イベントを受け取り、テストする
            response = _packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());

            // 研究完了イベントを検証（他のイベントも含まれる可能性がある）
            var researchCompleteEvents2 = eventMessagePack.Events
                .Where(e => e.Tag == ResearchCompleteEventPacket.EventTag)
                .ToList();

            Assert.AreEqual(1, researchCompleteEvents2.Count, "Should have exactly 1 research complete event after Research 2 completion");
            VerifyResearchCompleteEvent(researchCompleteEvents2[0], Research2Guid);
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