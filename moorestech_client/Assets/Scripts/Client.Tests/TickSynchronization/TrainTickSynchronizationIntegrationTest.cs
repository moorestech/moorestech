using System;
using System.Runtime.Serialization;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Client.Network.API;
using Client.Tests.Common;
using Game.Context;
using Game.PlayerRiding.Interface;
using Game.Train.RailGraph;
using Game.Train.Unit;
using Server.Event;
using Tests.UnitTest.PlayerRiding;
using Tests.Util;
using UniRx;
using Core.Update.TickSynchronization;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Train.Network;
using Client.Starter.Initialization;
using Core.Update;
using Cysharp.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Client.Tests.TickSynchronization
{
    public class TrainTickSynchronizationIntegrationTest
    {
        [Test]
        public void RealRailAndTrainPorts_ShareSequenceAndApplyEveryCapturedMutation()
        {
            var (packets, services) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using var serviceLifetime = services;
            var environment = new TrainTestEnvironment(services, ServerContext.WorldBlockDatastore, packets);
            using var client = new TrainSnapshotClientFixture();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            Assert.IsTrue(environment.GetTrainUnitDatastore().TryGetTrainUnitByCar(car.TrainCarInstanceId, out var train));
            var riding = services.GetRequiredService<IPlayerRidingDatastore>();
            Assert.AreEqual(RideActionResult.Success, riding.TryRide(1,
                new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive()), out _));
            var sink = new CapturedEventSink();
            services.GetRequiredService<EventProtocolProvider>().RegisterPlayer(1, sink);
            client.ApplyRail(sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag).Payload);
            var clientTrain = client.Trains.Upsert(TrainUnitSnapshotFactory.CreateSnapshot(train));
            var initialBranch = clientTrain.GetManualBranchSelectionIndex();
            sink.TakeAll();

            // 同一tickで入力・rail更新
            // Emit input and rail updates in one server tick.
            services.GetRequiredService<TrainCarRidingInputBuffer>().SetLatestInput(
                new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, 0, false, true, false, false));
            GameUpdater.UpdateOneTick();
            var graph = environment.GetRailGraphDatastore();
            var (first, second) = RailNode.CreatePairAndRegister(graph);
            graph.ConnectNode(first, second, 10, Guid.Empty, false);
            graph.DisconnectNode(first, second);
            graph.RemoveNode(second);
            var events = sink.TakeAll();
            Assert.AreEqual(6, events.Count);
            var ids = events.Select(GetId).ToArray();
            Assert.That(ids.Select(id => (uint)(id >> 32)), Is.All.EqualTo(1u));
            CollectionAssert.AreEqual(Enumerable.Range(1, events.Count).Select(seq => (uint)seq), ids.Select(id => (uint)id));
            Assert.AreEqual(events.Count, ids.Distinct().Count());
            Assert.AreEqual(initialBranch + 1, train.GetManualBranchSelectionIndex());

            var previousApi = ClientContext.VanillaApi;
            using var source = new Subject<EventMessagePack>();
            var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
            TestReflection.SetField(exchange, "_eventPacketSubject", source);
            var dispatcher = new VanillaApiEvent(exchange);
            var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
            typeof(VanillaApi).GetField("Event").SetValue(api, dispatcher);
            try
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
                using var nodes = new RailGraphCacheNetworkHandler(client.Context, client.Rails,
                    TestReflection.GetField<ClientStationReferenceRegistry>(client, "_stations"));
                using var connections = new RailGraphConnectionNetworkHandler(client.Context, client.Rails);
                using var bundles = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context, client.Trains);
                nodes.Initialize();
                connections.Initialize();
                bundles.Initialize();
                foreach (var message in events.AsEnumerable().Reverse()) source.OnNext(message);
                dispatcher.InitializeDispatch();
                Assert.AreEqual(initialBranch, clientTrain.GetManualBranchSelectionIndex());
                Assert.IsFalse(client.Rails.Nodes.Any(node => node != null && node.NodeGuid == first.Guid));

                // 逆順到着しても全keyを一度ずつ適用し、相殺される追加/削除の中間状態も観測する。
                // Flush every key once after reverse delivery and observe intermediate create/remove states.
                foreach (var message in events)
                {
                    var id = GetId(message);
                    Assert.IsTrue(client.Context.Events.TryFlushEvent(id), message.Tag);
                    Assert.IsFalse(client.Context.Events.TryFlushEvent(id), message.Tag);
                    switch (message.Tag)
                    {
                        case TrainUnitTickDiffBundleEventPacket.EventTag:
                            Assert.AreEqual(train.GetManualBranchSelectionIndex(), clientTrain.GetManualBranchSelectionIndex());
                            break;
                        case RailNodeCreatedEventPacket.EventTag:
                            var created = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(message.Payload);
                            Assert.AreEqual(created.NodeGuid, client.Rails.Nodes[created.NodeId].NodeGuid);
                            break;
                        case RailConnectionCreatedEventPacket.EventTag:
                            var connected = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(message.Payload);
                            Assert.IsTrue(client.Rails.ConnectNodes[connected.FromNodeId].Any(edge => edge.targetId == connected.ToNodeId && edge.distance == connected.Distance));
                            break;
                        case RailConnectionRemovedEventPacket.EventTag:
                            var disconnected = MessagePackSerializer.Deserialize<RailConnectionRemovedMessagePack>(message.Payload);
                            Assert.IsFalse(client.Rails.ConnectNodes[disconnected.FromNodeId].Any(edge => edge.targetId == disconnected.ToNodeId));
                            break;
                        case RailNodeRemovedEventPacket.EventTag:
                            var removed = MessagePackSerializer.Deserialize<RailNodeRemovedMessagePack>(message.Payload);
                            Assert.IsNull(client.Rails.Nodes[removed.NodeId]);
                            break;
                    }
                }
                Assert.AreEqual(graph.GetConnectNodesHash(), client.Rails.ComputeCurrentHash());
            }
            finally
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", previousApi);
            }

            #region Internal
            ulong GetId(EventMessagePack message)
            {
                switch (message.Tag)
                {
                    case TrainUnitTickDiffBundleEventPacket.EventTag:
                        var bundle = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(message.Payload);
                        Assert.IsNotEmpty(bundle.Diffs);
                        return TrainTickUnifiedIdUtility.CreateTickUnifiedId(bundle.ServerTick, bundle.DiffTickSequenceId);
                    case RailNodeCreatedEventPacket.EventTag:
                        var created = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(message.Payload);
                        return TrainTickUnifiedIdUtility.CreateTickUnifiedId(created.ServerTick, created.TickSequenceId);
                    case RailNodeRemovedEventPacket.EventTag:
                        var removed = MessagePackSerializer.Deserialize<RailNodeRemovedMessagePack>(message.Payload);
                        return TrainTickUnifiedIdUtility.CreateTickUnifiedId(removed.ServerTick, removed.TickSequenceId);
                    case RailConnectionCreatedEventPacket.EventTag:
                        var connected = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(message.Payload);
                        return TrainTickUnifiedIdUtility.CreateTickUnifiedId(connected.ServerTick, connected.TickSequenceId);
                    case RailConnectionRemovedEventPacket.EventTag:
                        var disconnected = MessagePackSerializer.Deserialize<RailConnectionRemovedMessagePack>(message.Payload);
                        return TrainTickUnifiedIdUtility.CreateTickUnifiedId(disconnected.ServerTick, disconnected.TickSequenceId);
                    default:
                        throw new AssertionException("Unexpected event: " + message.Tag);
                }
            }
            #endregion
        }

        [Test]
        public void CapturedHandshakeAndEmptyBundle_ApplyThenAdvanceExactlyOnce()
        {
            var (packets, services) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using (services)
            using (var client = new TrainSnapshotClientFixture())
            {
                var sink = new CapturedEventSink();
                var request = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(1, "Tick test"));
                packets.GetPacketResponse(request, new PacketResponseContext(sink));
                var waiting = InitialEventApplyWaiter.WaitAllAsync(new IInitialEventApplyWaitTarget[] { client.Handler }).Preserve();
                Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);

                // railだけでは起動待機を完了せず、trainのview適用後に完了する。
                // Rail alone cannot finish startup; train view application must complete first.
                new Client.Game.InGame.Train.Unit.TrainUnitClientSimulator(client.Context, client.Gate, null).Tick();
                client.ApplyRail(sink.Events[0].Payload);
                Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
                client.ApplyTrain(sink.Events[1].Payload);
                Assert.AreEqual(UniTaskStatus.Succeeded, waiting.Status);
                // terrain前の個別待機後も全target待機が同じcompletionを再観測できる。
                // The all-target wait can observe the same completion after the pre-terrain wait.
                client.Handler.WaitForInitialApplyAsync().GetAwaiter().GetResult();
                InitialEventApplyWaiter.WaitAllAsync(new IInitialEventApplyWaitTarget[] { client.Handler }).GetAwaiter().GetResult();
                Assert.IsTrue(client.Context.IsInitialSnapshotApplied);
                Assert.IsEmpty(client.Rails.Nodes);
                Assert.IsEmpty(client.Trains.Units);

                GameUpdater.UpdateOneTick();
                var payload = sink.Events.Single(e => e.Tag == TrainUnitTickDiffBundleEventPacket.EventTag).Payload;
                var bundle = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(payload);
                Assert.IsEmpty(bundle.Diffs);
                var handler = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context, client.Trains);
                TrainSnapshotClientFixture.Receive(handler, "OnEventReceived", payload);
                client.Context.AdvanceController.Advance(0.1f, client.Gate);
                Assert.AreEqual(1u, client.Context.State.GetTick());
                client.Context.AdvanceController.Advance(0.1f, client.Gate);
                Assert.IsFalse(client.Context.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, bundle.DiffTickSequenceId)));
                Assert.AreEqual(1u, client.Context.State.GetTick());
            }
        }

        [Test]
        public void FullSnapshotWatermark_DropsCapturedOldBundleAndRetainsNewBundle()
        {
            var (packets, services) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using (services)
            using (var client = new TrainSnapshotClientFixture())
            {
                var sink = EventTestUtil.RegisterCaptureSink(services, 1);
                GameUpdater.UpdateOneTick();
                services.GetRequiredService<EventProtocolProvider>().RegisterPlayer(1, sink);
                GameUpdater.UpdateOneTick();
                var bundles = sink.Events.Where(e => e.Tag == TrainUnitTickDiffBundleEventPacket.EventTag).ToArray();
                var handler = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context, client.Trains);
                foreach (var bundle in bundles) TrainSnapshotClientFixture.Receive(handler, "OnEventReceived", bundle.Payload);

                // snapshotを跨いで到着済みの2本を、watermarkで片方だけ捨てる。
                // Purge only the older of two already-received bundles at the snapshot watermark.
                client.ApplyRail(sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag).Payload);
                client.ApplyTrain(sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag).Payload);
                Assert.AreEqual(1u, client.Context.State.GetTick());
                Assert.IsFalse(client.Context.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
                Assert.IsTrue(client.Context.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(2, 1)));
                Assert.IsFalse(client.Context.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(2, 1)));
            }
        }
    }
}
