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
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Server.Util.MessagePack;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using Client.Tests.Inventory;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltInitialEventBufferTest
    {
        [UnityTest]
        public IEnumerator BufferedBeforeSubscriptionAndAfterSnapshotEventsApplyOnceTest() => UniTask.ToCoroutine(async () =>
        {
            var exchange = new PacketExchangeManager(null);
            var events = new VanillaApiEvent(exchange);
            Send(10); Send(11);
            await UniTask.Yield(); await UniTask.Yield();
            var buffer = BeltTestState.Buffer(out var state);
            state.RecordAppliedTickUnifiedId(10, 0);
            var handler = new BeltNetworkEventHandler(Initial(), events, buffer);
            int notifications = 0;
            using var subscription = handler.Replica.OnStateChanged.Subscribe(_ => notifications++);
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
            events.InitializeDispatch();
            Assert.AreEqual(0, notifications, "Receipt must not advance the shared simulation.");
            BeltTestState.FlushTick(buffer, state, 10);
            Assert.AreEqual(2, state.GetTickSequenceId(), "Snapshot-covered events must still consume their sequences.");
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
                var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(BeltTestState.Tick(tick)));
                exchange.EnqueueReceivedPacket(MessagePackSerializer.Serialize(new EventStreamMessagePack(new EventMessagePack(BeltTickCompletedEventPacket.EventTag, payload))));
            }
            #endregion
        });
        [Test]
        public void SharedSequenceInterleavesTrainSpeedMachineSimulationAndRailTest()
        {
            var buffer = BeltTestState.Buffer(out var state);
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(Initial(), events, buffer);
            var speed = new BeltSpeedChange(new[] { new BeltCellSpeed(1, 64) });
            var replacement = new BeltCellItemsChange(1, BeltTestState.Snapshot(17, 64, true).Items);
            var order = new BeltTickOrder(11, new uint[] { 2 }, 4, new uint[] { 6 }, 7);
            var difference = new BeltTickDifference(11, new BeltBoundaryChange[] { speed }, Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { replacement }, order);
            // 到着順が逆でも、速度確定・機械処理・搬送・末尾変更をseq順に適用する。
            // Apply speed, machine work, transport and final changes in sequence even when received out of order.
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(difference)));
            Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress);
            int callbacks = 0, notifications = 0;
            using var subscription = handler.Replica.OnStateChanged.Subscribe(_ => notifications++);
            buffer.EnqueueEvent(11, 1, TrainTickBufferedEvent.Create(() =>
            {
                Assert.AreEqual(32, handler.Replica.Snapshot.Cells[0].Speed);
                Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress); callbacks++;
            }));
            buffer.EnqueueEvent(11, 3, TrainTickBufferedEvent.Create(() =>
            {
                Assert.AreEqual(64, handler.Replica.Snapshot.Cells[0].Speed);
                Assert.AreEqual(1, handler.Replica.Snapshot.Items[0].Progress); callbacks++;
            }));
            buffer.EnqueueEvent(11, 5, TrainTickBufferedEvent.Create(() =>
            {
                Assert.AreEqual(65, handler.Replica.Snapshot.Items[0].Progress);
                Assert.AreEqual(0, notifications); callbacks++;
            }));
            BeltTestState.FlushTick(buffer, state, 11);
            Assert.AreEqual(7, state.GetTickSequenceId());
            Assert.AreEqual(3, callbacks); Assert.AreEqual(1, notifications);
            Assert.AreEqual(17, handler.Replica.Snapshot.Items[0].Progress);
        }
        [Test]
        public void LaterRailSequenceWaitsForBeltBundleAndThenResumesTest()
        {
            var buffer = BeltTestState.Buffer(out var state);
            state.Initialize(TrainTickUnifiedIdUtility.CreateTickUnifiedId(11, 1));
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(Initial(), events, buffer);
            var rail = RailGraphClientCache.CreateForEditorTest();
            var diagnostics = new TrainSynchronizationDiagnostics(state, new TrainSynchronizationDiagnosticWriter(Application.temporaryCachePath));
            var gate = new TrainUnitHashVerifier(buffer, new TrainUnitClientCache(rail), rail, state, diagnostics);
            // railのseq3が先着し、tick末尾にまとめたベルトのseq2がまだ届いていない。
            // Rail sequence 3 arrives before belt sequence 2, which is bundled at tick end.
            buffer.EnqueueEvent(11, 3, TrainTickBufferedEvent.Create(() => { }));
            Assert.IsFalse(gate.CanAdvanceTick(TrainTickUnifiedIdUtility.CreateTickUnifiedId(11, 2)));
            Assert.IsFalse(state.IsPermanentlyWaiting, "A later sequence does not prove an earlier tick bundle was lost.");
            var change = new BeltSpeedChange(new[] { new BeltCellSpeed(1, 64) });
            var difference = new BeltTickDifference(11, new BeltBoundaryChange[] { change }, Array.Empty<BeltOutputResult>(), Array.Empty<BeltBoundaryChange>(),
                new BeltTickOrder(11, new uint[] { 2 }, 4, Array.Empty<uint>(), 5));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(difference)));
            BeltTestState.FlushTick(buffer, state, 11);
            Assert.AreEqual(5, state.GetTickSequenceId());
            Assert.AreEqual(65, handler.Replica.Snapshot.Items[0].Progress);
            buffer.EnqueueHash(TrainUnitFutureMessageBuffer.DummyHash, TrainUnitFutureMessageBuffer.DummyHash, 11, 6);
            Assert.IsTrue(gate.CanAdvanceTick(TrainTickUnifiedIdUtility.CreateTickUnifiedId(11, 6)));
        }
        internal static InitialHandshakeResponse Initial()
        {
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new HandshakeAcceptedMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null, 1));
            var snapshot = new BeltSnapshotMessagePack(new BeltCommittedSnapshot(10, BeltTestState.Snapshot(1, 32, true)));
            return new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, default, snapshot));
        }
    }
}
