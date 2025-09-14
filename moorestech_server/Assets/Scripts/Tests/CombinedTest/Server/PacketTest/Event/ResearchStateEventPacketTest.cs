using System;
using Game.Research;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using UniRx;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    [TestFixture]
    public class ResearchStateEventPacketTest
    {
        private ResearchEvent _researchEvent;
        private EventProtocolProvider _eventProtocolProvider;
        private ResearchStateEventPacket _researchStateEventPacket;

        [SetUp]
        public void SetUp()
        {
            _researchEvent = new ResearchEvent();
            _eventProtocolProvider = new EventProtocolProvider();
            _researchStateEventPacket = new ResearchStateEventPacket(_eventProtocolProvider, _researchEvent);

            // イベントキューをクリア
            while (_eventProtocolProvider.EventPackets.Count > 0)
            {
                _eventProtocolProvider.EventPackets.Dequeue();
            }
        }

        [Test]
        public void ResearchCompleted_イベントが発生した時_パケットがキューに追加される()
        {
            // Arrange
            var playerId = 1;
            var researchGuid = Guid.NewGuid();

            // Act
            _researchEvent.PublishResearchCompleted(playerId, researchGuid);

            // Assert
            Assert.AreEqual(1, _eventProtocolProvider.EventPackets.Count);
            var packet = _eventProtocolProvider.EventPackets.Dequeue() as ResearchStateEventPacket.ResearchStateEventMessagePack;
            Assert.IsNotNull(packet);
            Assert.AreEqual(ResearchStateEventPacket.EventTag, packet.Tag);
            Assert.AreEqual(researchGuid.ToString(), packet.Data.ResearchGuidStr);
            Assert.IsTrue(packet.Data.IsCompleted);
            Assert.AreEqual(playerId, packet.Data.PlayerId);
        }

        [Test]
        public void ResearchFailed_イベントが発生した時_パケットがキューに追加される()
        {
            // Arrange
            var playerId = 1;
            var researchGuid = Guid.NewGuid();
            var reason = "Not enough items";

            // Act
            _researchEvent.PublishResearchFailed(playerId, researchGuid, reason);

            // Assert
            Assert.AreEqual(1, _eventProtocolProvider.EventPackets.Count);
            var packet = _eventProtocolProvider.EventPackets.Dequeue() as ResearchStateEventPacket.ResearchStateEventMessagePack;
            Assert.IsNotNull(packet);
            Assert.AreEqual(ResearchStateEventPacket.EventTag, packet.Tag);
            Assert.AreEqual(researchGuid.ToString(), packet.Data.ResearchGuidStr);
            Assert.IsFalse(packet.Data.IsCompleted);
            Assert.AreEqual(playerId, packet.Data.PlayerId);
            Assert.AreEqual(reason, packet.Data.FailureReason);
        }
    }
}