using System.Linq;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive.BeltTransportSync;
using Tests.UnitTest.Game.BeltConnection.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync
{
    // 接続登録時のベルト全量pushが、その接続だけへ1件・その時点の全量・現在tickと発行済み連番のウォーターマークで届き、連番を消費しないか
    // Whether the registration-time belt full state push reaches only that connection once, with the current full state, current tick and the issued-id watermark, without consuming a sequence id
    public class BeltTransportFullSnapshotEventPacketTest
    {
        private const int OtherPlayerId = 1;
        private const int JoiningPlayerId = 2;

        [Test]
        public void RegistrationPushesOneCurrentBeltSnapshotToThatPlayerOnly()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var other = EventTestUtil.RegisterCaptureSink(serviceProvider, OtherPlayerId);

            var expected = CaptureServer();
            Assert.Greater(BeltSyncTestUtil.ItemCount(expected), 0, "items are on the belts after the warm-up");
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(JoiningPlayerId, sink);

            var snapshots = sink.Events.Where(e => e.Tag == BeltTransportFullSnapshotEventPacket.EventTag).ToList();
            Assert.AreEqual(1, snapshots.Count, "belt snapshots pushed to the joining player");
            var message = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(snapshots[0].Payload);
            BeltFullStateAssert.AreEqual(expected, message.ToFullState());
            Assert.AreEqual(trainUpdateService.GetCurrentTick(), message.ServerTick, "server tick");
            Assert.AreEqual(trainUpdateService.GetCurrentTickSequenceId(), message.TickSequenceId, "watermark");
            Assert.IsFalse(other.Events.Any(e => e.Tag == BeltTransportFullSnapshotEventPacket.EventTag), "other players get no snapshot");
        }

        [Test]
        public void RegistrationPushDoesNotConsumeSequenceId()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            // 暖機後は列車diffとベルトで連番が進んでいる。登録pushの前後で変わらない
            // After the warm-up the train diff and belt event advanced the counter; it stays the same across the registration push
            var before = trainUpdateService.GetCurrentTickSequenceId();
            Assert.Greater(before, 0u, "sequence ids were issued this tick");
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(JoiningPlayerId, sink);

            Assert.AreEqual(1, sink.Events.Count(e => e.Tag == BeltTransportFullSnapshotEventPacket.EventTag), "belt snapshot pushed");
            Assert.AreEqual(before, trainUpdateService.GetCurrentTickSequenceId(), "sequence id after the push");
        }
    }
}
