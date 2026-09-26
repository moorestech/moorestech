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
                client.ApplyRail(sink.Events[0].Payload);
                Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
                client.ApplyTrain(sink.Events[1].Payload);
                Assert.AreEqual(UniTaskStatus.Succeeded, waiting.Status);
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
                Assert.IsFalse(client.Context.Events.TryFlushEvent(1, bundle.DiffTickSequenceId));
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
                services.GetRequiredService<TrainFullSnapshotEventPacket>().PushFullSnapshots(1, true);
                GameUpdater.UpdateOneTick();
                var bundles = sink.Events.Where(e => e.Tag == TrainUnitTickDiffBundleEventPacket.EventTag).ToArray();
                var handler = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context, client.Trains);
                foreach (var bundle in bundles) TrainSnapshotClientFixture.Receive(handler, "OnEventReceived", bundle.Payload);

                // snapshotを跨いで到着済みの2本を、watermarkで片方だけ捨てる。
                // Purge only the older of two already-received bundles at the snapshot watermark.
                client.ApplyRail(sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag).Payload);
                client.ApplyTrain(sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag).Payload);
                Assert.AreEqual(1u, client.Context.State.GetTick());
                Assert.IsFalse(client.Context.Events.TryFlushEvent(1, 1));
                Assert.IsTrue(client.Context.Events.TryFlushEvent(2, 1));
                Assert.IsFalse(client.Context.Events.TryFlushEvent(2, 1));
            }
        }
    }
}
