using UniRx;
using System.Collections;
using Client.Game.InGame.BeltTransport;
using Client.Network.API;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Util.MessagePack;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine.TestTools;
using System;
using Client.Tests.Inventory;
using Client.Game.InGame.Train.Network;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltInitialEventBufferTest
    {
        [UnityTest]
        public IEnumerator BufferedBeforeSubscriptionAndAfterSnapshotEventsApplyOnceTest() => UniTask.ToCoroutine(async () =>
        {
            var exchange = new PacketExchangeManager(null);
            var events = new VanillaApiEvent(exchange);
            Send(10);
            var snapshotPayload = MessagePackSerializer.Serialize(new BeltSnapshotMessagePack(new BeltCommittedSnapshot(10, BeltTestState.Snapshot(1, 32, true))));
            exchange.EnqueueReceivedPacket(MessagePackSerializer.Serialize(new EventStreamMessagePack(new EventMessagePack(TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag, snapshotPayload))));
            exchange.EnqueueReceivedPacket(MessagePackSerializer.Serialize(new EventStreamMessagePack(new EventMessagePack(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, Array.Empty<byte>()))));
            Send(11);
            await UniTask.Yield(); await UniTask.Yield();
            var buffer = BeltTestState.Buffer(out var state);
            state.RecordAppliedTickUnifiedId(10, 0);
            var handler = new BeltNetworkEventHandler(events, buffer);
            int notifications = 0;
            using var subscription = handler.OnStateChanged.Subscribe(_ => notifications++);
            Assert.AreEqual(0, notifications);
            events.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, _ =>
            {
                var watermark = ((ulong)10 << 32) | 1;
                buffer.DiscardEventsAtOrBelow(watermark);
                state.Initialize(watermark);
            });
            events.InitializeDispatch();
            Assert.AreEqual(0, notifications, "Receipt must not advance the shared simulation.");
            Assert.AreEqual(1, state.GetTickSequenceId(), "The train watermark must exclude snapshot-covered belt events.");
            BeltTestState.FlushTick(buffer, state, 11);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(33, handler.Replica.Snapshot.Items[0].Progress);
            Send(12);
            await UniTask.Yield(); await UniTask.Yield();
            Assert.AreEqual(1, notifications);
            BeltTestState.FlushTick(buffer, state, 12);
            Assert.AreEqual(2, notifications);
            Assert.AreEqual(BeltTestState.Identity, handler.Replica.Snapshot.Items[0].Item.Guid);
            Assert.AreEqual(65, handler.Replica.Snapshot.Items[0].Progress);

            #region Internal
            void Send(ulong tick)
            {
                var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(BeltTestState.Tick(tick), (uint)tick, 1));
                exchange.EnqueueReceivedPacket(MessagePackSerializer.Serialize(new EventStreamMessagePack(new EventMessagePack(BeltTickCompletedEventPacket.EventTag, payload))));
            }
            #endregion
        });
        [Test]
        public void SharedSequenceAppliesCompletedBeltTickBetweenTrainAndRailTest()
        {
            var buffer = BeltTestState.Buffer(out var state);
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(events, buffer);
            SendInitial(events);
            var speed = new BeltSpeedChange(new[] { new BeltCellSpeed(1, 64) });
            var replacement = new BeltCellItemsChange(1, BeltTestState.Snapshot(17, 64, true).Items);
            var difference = new BeltTickDifference(11, new BeltBoundaryChange[] { speed }, Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { replacement });
            int callbacks = 0, notifications = 0;
            using var subscription = handler.OnStateChanged.Subscribe(_ => notifications++);
            // 送信順のseqで、前後のイベントと完了tickを適用する。
            // Apply surrounding events and the completed tick in send-order sequence.
            buffer.EnqueueEvent(11, 1, TrainTickBufferedEvent.Create(() =>
            {
                Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
                Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress); callbacks++;
            }));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(difference, 11, 2)));
            buffer.EnqueueEvent(11, 3, TrainTickBufferedEvent.Create(() =>
            {
                Assert.AreEqual(64, handler.Replica.Snapshot.Cells[0].Speed);
                Assert.AreEqual(17, handler.Replica.Snapshot.Items[0].Progress);
                Assert.AreEqual(1, notifications); callbacks++;
            }));
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
            BeltTestState.FlushTick(buffer, state, 11);
            Assert.AreEqual(3, state.GetTickSequenceId());
            Assert.AreEqual(2, callbacks); Assert.AreEqual(1, notifications);
        }
        internal static void SendInitial(CapturingVanillaApiEvent events)
        {
            var snapshot = new BeltSnapshotMessagePack(new BeltCommittedSnapshot(10, BeltTestState.Snapshot(1, 32, true)));
            events.Dispatch(TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag, MessagePackSerializer.Serialize(snapshot));
        }
    }
}
