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
            Assert.AreEqual(BeltTestState.Identity, handler.Replica.Snapshot.Items[0].Item.Guid);
            Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DuplicateTopologyEdgesAreRejectedBeforeValidSpeedChangeTest(bool removed)
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            var edge = new BeltConnectionMessagePack(1, 2, true, true, BeltDirection.Front, 0);
            var duplicates = new[] { edge, edge, edge, edge };
            var cell = new BeltCellMessagePack(new BeltNetworkCell(2, -2, 3, 5, 32, "fixed:1", BeltDirection.Front, new BeltCellSurfaceProfile(0, 0)));
            var topology = new BeltTopologyChangeMessagePack(new[] { cell }, Array.Empty<int>(),
                removed ? Array.Empty<BeltConnectionMessagePack>() : duplicates,
                removed ? duplicates : Array.Empty<BeltConnectionMessagePack>(), Array.Empty<BeltCellItemMessagePack>());
            var packet = new BeltTickMessagePack(11, new BeltChangeMessagePack[] { new BeltSpeedChangeMessagePack(new[] { new BeltSpeedMessagePack(1, 64) }) },
                Array.Empty<BeltOutputMessagePack>(), new BeltChangeMessagePack[] { topology });
            LogAssert.Expect(LogType.Error, new Regex("Belt transport packet failed:"));
            Assert.DoesNotThrow(() => events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(packet)));
            Assert.Throws<InvalidOperationException>(handler.ThrowIfFailed);
            Assert.AreEqual(1, handler.Replica.Snapshot.Cells.Length);
            Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
            Assert.AreEqual(BeltTestState.Identity, handler.Replica.Snapshot.Items[0].Item.Guid);
        }
        [TestCase(4, false)]
        [TestCase(4, true)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        public void IncomingPortStructureIsValidatedBeforeReplayTest(int count, bool distinctDirections)
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            var cells = new BeltCellMessagePack[5];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new BeltCellMessagePack(new BeltNetworkCell(i + 2, i, 0, 0, 32, "fixed:1", BeltDirection.Front, new BeltCellSurfaceProfile(0, 0)));
            var edges = new BeltConnectionMessagePack[count];
            var directions = new[] { BeltDirection.Front, BeltDirection.Left, BeltDirection.Right, BeltDirection.Back };
            for (int i = 0; i < count; i++) edges[i] = new BeltConnectionMessagePack(i + 2, 6, true, true, distinctDirections ? directions[i] : BeltDirection.Front, 0);
            var topology = new BeltTopologyChangeMessagePack(cells, Array.Empty<int>(), edges, Array.Empty<BeltConnectionMessagePack>(), Array.Empty<BeltCellItemMessagePack>());
            var packet = new BeltTickMessagePack(11, new BeltChangeMessagePack[] { new BeltSpeedChangeMessagePack(new[] { new BeltSpeedMessagePack(1, 64) }) },
                Array.Empty<BeltOutputMessagePack>(), new BeltChangeMessagePack[] { topology });
            bool valid = count == 3 && distinctDirections;
            if (!valid) LogAssert.Expect(LogType.Error, new Regex("Belt transport packet failed:"));
            Assert.DoesNotThrow(() => events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(packet)));
            if (valid)
            {
                Assert.DoesNotThrow(handler.ThrowIfFailed);
                Assert.AreEqual(6, handler.Replica.Snapshot.Cells.Length);
                Assert.AreEqual(65, handler.Replica.Snapshot.Items[0].Progress);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(handler.ThrowIfFailed);
                Assert.AreEqual(1, handler.Replica.Snapshot.Cells.Length);
                Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
                Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
            }
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
            Assert.AreEqual(33, handler.Replica.Snapshot.Items[0].Progress);
        }
    }
}
