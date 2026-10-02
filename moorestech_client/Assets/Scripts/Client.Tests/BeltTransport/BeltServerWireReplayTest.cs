using UniRx;
using System;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Train.Network;
using Client.Network.API;
using Client.Tests.Inventory;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack.BeltTransport;
using Server.Util.MessagePack;
using Server.Protocol.PacketResponse.Handshake;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltServerWireReplayTest
    {
        [Test]
        public void ActualSnapshotProtocolAndCompletedEventsReproduceServerTest()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var transport = services.GetRequiredService<BeltWorldTransport>();
            var source = Place(ForUnitTestModBlockId.ChestId, 0).GetComponent<IBlockInventory>();
            Place(ForUnitTestModBlockId.BeltConveyorId, 1);
            Place(ForUnitTestModBlockId.BeltConveyorId, 2);
            var target = Place(ForUnitTestModBlockId.ChestId, 3).GetComponent<IBlockInventory>();
            transport.Initialize();
            var sink = EventTestUtil.RegisterCaptureSink(services, 1);
            GameUpdater.UpdateOneTick();
            var initialPackets = sink.TakeAll();
            var snapshotPacket = initialPackets.Single(packet => packet.Tag == TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag);
            var initialSnapshot = MessagePackSerializer.Deserialize<BeltSnapshotMessagePack>(snapshotPacket.Payload);
            var trainPacket = initialPackets.Single(packet => packet.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag);
            var trainSnapshot = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(trainPacket.Payload);
            var buffer = BeltTestState.Buffer(out var state);
            state.Initialize(((ulong)trainSnapshot.ServerTick << 32) | trainSnapshot.WatermarkTickSequenceId);
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(events, buffer);
            events.Dispatch(snapshotPacket.Tag, snapshotPacket.Payload);
            Assert.AreEqual(GameUpdater.CurrentTick, initialSnapshot.Tick);
            var replica = handler.Replica;
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            int count = 0, notifications = 0, trainTicks = 0, railEvents = 0;
            var railGraph = services.GetRequiredService<IRailGraphDatastore>();
            GameUpdater.TickEndUpdates.Add(CreateRailNode);

            using var subscription = replica.OnStateChanged.Subscribe(_ => notifications++);
            // 実搬送をwire往復後にCPU再現。
            // Replay actual transport on the CPU after the wire round trip.
            for (int i = 0; i < 250; i++)
            {
                GameUpdater.UpdateOneTick();
                BeltTickMessagePack completed = null;
                uint sentSequence = 0;
                foreach (var packet in sink.TakeAll())
                {
                    if (packet.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
                    {
                        var train = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(packet.Payload);
                        Assert.AreEqual(++sentSequence, train.DiffTickSequenceId, "Train seq must follow actual send order.");
                        buffer.EnqueueEvent(train.ServerTick, train.DiffTickSequenceId, TrainTickBufferedEvent.Create(() =>
                        {
                            Assert.AreEqual(count, notifications, "Train must advance before this tick's belt simulation.");
                            trainTicks++;
                        }));
                    }
                    if (packet.Tag == RailNodeCreatedEventPacket.EventTag)
                    {
                        var rail = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(packet.Payload);
                        Assert.AreEqual(++sentSequence, rail.TickSequenceId, "Rail seq must follow actual send order.");
                        buffer.EnqueueEvent(rail.ServerTick, rail.TickSequenceId, TrainTickBufferedEvent.Create(() => railEvents++));
                    }
                    if (packet.Tag != BeltTickCompletedEventPacket.EventTag) continue;
                    completed = MessagePackSerializer.Deserialize<BeltTickMessagePack>(packet.Payload);
                    Assert.AreEqual(++sentSequence, completed.TickSequenceId, "Belt seq must be allocated when sending, after tick-end rail events.");
                    events.Dispatch(packet.Tag, packet.Payload);
                }
                Assert.NotNull(completed);
                Assert.AreEqual(count, notifications, "Receiving a completed tick must not advance items.");
                BeltTestState.FlushTick(buffer, state, completed.ServerTick);
                Assert.AreEqual(completed.TickSequenceId, state.GetTickSequenceId(), "Shared seq must have no gaps.");
                count++;
                Assert.AreEqual(count, trainTicks);
                Assert.AreEqual(count, notifications);
                Assert.AreEqual(initialSnapshot.Tick + (ulong)count, transport.CaptureCommittedSnapshot().Tick);
                CollectionAssert.AreEqual(transport.CaptureCommittedSnapshot().Snapshot.Items, replica.Snapshot.Items);
            }
            GameUpdater.TickEndUpdates.Remove(CreateRailNode);
            Assert.AreEqual(1, railEvents);
            Assert.AreEqual(250, count);
            Assert.AreEqual(3, target.GetItem(0).Count);
            var legacy = (RequestWorldDataProtocol.ResponseWorldDataMessagePack)new RequestWorldDataProtocol(services).GetResponse(MessagePackSerializer.Serialize(new RequestWorldDataProtocol.RequestWorldDataMessagePack()), 1);
            Assert.IsEmpty(legacy.Entities);

            #region Internal
            void CreateRailNode()
            {
                // 搬送後・通知前のレールイベントで先行採番を検出する。
                // Detect premature belt sequence allocation with a rail event after transport and before publication.
                if (count == 0) railGraph.AddNodeSingle(new RailNode(railGraph));
            }
            IBlock Place(Core.Master.BlockId id, int z)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, new Vector3Int(0, 0, z), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
                return block;
            }
            #endregion
        }
    }
}
