using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport;
using Client.Tests.Inventory;
using Core.BeltTransport;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltPacketFailureBoundaryTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void InvalidAfterStageIsRejectedBeforeAnyTickMutationTest(int malformed)
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            BeltChangeMessagePack[] after = malformed switch
            {
                0 => null,
                1 => new BeltChangeMessagePack[] { new BeltInputChangeMessagePack(1, (BeltDirection)7, 1, new(new BeltItem(BeltTestState.Identity, 1))) },
                2 => new BeltChangeMessagePack[] { new BeltSpeedChangeMessagePack(new[] { new BeltSpeedMessagePack(1, 129) }) },
                _ => new BeltChangeMessagePack[] { new BeltTopologyChangeMessagePack(null, Array.Empty<int>(), Array.Empty<BeltConnectionMessagePack>(), Array.Empty<BeltConnectionMessagePack>(), Array.Empty<BeltCellItemMessagePack>()) }
            };
            var packet = new BeltTickMessagePack(11, new BeltChangeMessagePack[] { new BeltSpeedChangeMessagePack(new[] { new BeltSpeedMessagePack(1, 64) }) }, Array.Empty<BeltOutputMessagePack>(), after);
            LogAssert.Expect(LogType.Error, new Regex("Belt transport packet failed:"));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(packet));
            Assert.Throws<InvalidOperationException>(handler.ThrowIfFailed);
            Assert.AreEqual(10, handler.Replica.Tick);
            Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
        }
        [Test]
        public void InternalNotificationFailureIsNotReclassifiedAsPacketFailureTest()
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            using var subscription = handler.Replica.OnStateChanged.Subscribe(_ => throw new InvalidOperationException("Internal notification fixture"));
            var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(BeltTestState.Tick(11)));
            Assert.Throws<InvalidOperationException>(() => events.Dispatch(BeltTickCompletedEventPacket.EventTag, payload));
            Assert.DoesNotThrow(handler.ThrowIfFailed);
            Assert.AreEqual(11, handler.Replica.Tick);
        }
    }
}
