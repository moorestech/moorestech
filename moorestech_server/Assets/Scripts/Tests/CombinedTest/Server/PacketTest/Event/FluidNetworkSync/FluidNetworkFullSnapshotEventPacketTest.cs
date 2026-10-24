using System.Linq;
using Core.Update;
using Game.Fluid.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive.FluidNetworkSync;
using Tests.UnitTest.Game.Fluid.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync.FluidSyncPacketTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync
{
    // 接続登録時の流体全量pushが、その接続だけへ1件・その時点の全量・現在tickと発行済み連番のウォーターマークで届き、連番を消費しないか
    // Whether the registration-time fluid full state push reaches only that connection once, with the current full state, current tick and the issued-id watermark, without consuming a sequence id
    public class FluidNetworkFullSnapshotEventPacketTest
    {
        private const int OtherPlayerId = 1;
        private const int JoiningPlayerId = 2;

        [Test]
        public void RegistrationPushesOneCurrentFluidSnapshotToThatPlayerOnly()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var other = EventTestUtil.RegisterCaptureSink(serviceProvider, OtherPlayerId);

            var expected = CaptureServer();
            Assert.Greater(expected.Nodes.Sum(n => n.Amount), 0, "the pump has filled the pipes after the warm-up");
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(JoiningPlayerId, sink);

            var snapshots = sink.Events.Where(e => e.Tag == FluidNetworkFullSnapshotEventPacket.EventTag).ToList();
            Assert.AreEqual(1, snapshots.Count, "fluid snapshots pushed to the joining player");
            var message = MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(snapshots[0].Payload);
            FluidFullStateAssert.AreEqual(expected, message.ToFullState());
            Assert.AreEqual(trainUpdateService.GetCurrentTick(), message.ServerTick, "server tick");
            Assert.AreEqual(trainUpdateService.GetCurrentTickSequenceId(), message.TickSequenceId, "watermark");
            Assert.IsFalse(other.Events.Any(e => e.Tag == FluidNetworkFullSnapshotEventPacket.EventTag), "other players get no snapshot");
        }

        [Test]
        public void RegistrationPushDoesNotConsumeSequenceId()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            // tickの境界では連番は0に戻っている。登録pushはそれを進めず、次tickの番号付け(列車diff=1・ベルト=2・流体=3)もずれない
            // At the tick boundary the counter is back at 0; the registration push does not advance it, and the next tick's numbering (train diff 1, belt 2, fluid 3) is unshifted
            Assert.AreEqual(0u, trainUpdateService.GetCurrentTickSequenceId(), "no id issued at the boundary");
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(JoiningPlayerId, sink);

            Assert.AreEqual(1, sink.Events.Count(e => e.Tag == FluidNetworkFullSnapshotEventPacket.EventTag), "fluid snapshot pushed");
            Assert.AreEqual(0u, trainUpdateService.GetCurrentTickSequenceId(), "sequence id after the push");
            sink.TakeAll();
            GameUpdater.UpdateOneTick();
            var fluidEvent = sink.Events.Single(IsFluidTickEvent);
            Assert.AreEqual(FluidNetworkTickEventPacket.TickDiffEventTag, fluidEvent.Tag, "a non-rebuilding tick sends only the diff");
            Assert.AreEqual(3u, MessagePackSerializer.Deserialize<FluidNetworkTickDiffMessagePack>(fluidEvent.Payload).TickSequenceId, "fluid diff keeps sequence id 3 after the push");
        }
    }
}

