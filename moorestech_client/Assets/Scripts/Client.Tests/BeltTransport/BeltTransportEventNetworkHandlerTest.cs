using System.Collections.Generic;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.UnitTest.Game.BeltConnection.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Client.Tests.BeltTransport
{
    // サーバーが送った途中参加の全量・毎tickの差分・再構築の全量を、クライアントの受信口から列車の順序待ちキューへ通して流し、複製がサーバーと一致し、検証器が列車束のベルトハッシュを通すか
    // Whether the server's join-time full state, per-tick diffs and rebuilt full states, fed through the client receive port into the train's ordered buffer, keep the replica equal to the server and pass the verifier against the belt hash on the train bundle
    public class BeltTransportEventNetworkHandlerTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        // 8tick暖機後、24回目の前の設置は再構築tick32(ハッシュtick)に、41回目の前の設置はハッシュtick48の直後に当たる
        // After an 8-tick warm-up, the placement before iteration 24 rebuilds at tick 32 (a hash tick) and the one before iteration 41 lands right after hash tick 48
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void ReplicaFedThroughTheOrderedBufferMatchesServerAndPassesTheVerifier()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);

            var rig = new BeltSyncClientRig();
            rig.Join(sink.TakeAll());
            Assert.AreEqual(BeltTransportStateHash.Compute(CaptureServer()), rig.Cache.ComputeCurrentHash(), "replica hash right after the join-time full state");

            var expectedRebuiltTicks = new List<uint>();
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) rig.Receive(eventMessagePack);
                rig.FlushLikeTheSimulator();

                // 毎tick、前tickのハッシュを検証器が通し、ベルトの連番まで流れた複製はサーバーと同じ状態
                // Every tick the verifier passes the previous tick's hash, and once flushed up to the belt's sequence id the replica equals the server
                var tick = trainUpdateService.GetCurrentTick();
                Assert.AreEqual(tick, rig.TickState.GetTick(), $"client reached tick {tick}");
                Assert.AreEqual(BeltTransportStateHash.Compute(CaptureServer()), rig.Cache.ComputeCurrentHash(), $"replica hash at tick {tick}");
            }

            Assert.AreEqual(expectedRebuiltTicks.Count, rig.RebuiltFullStatesReceived, "one rebuilt full state per placement");
            Assert.AreEqual(TickCount, rig.TrainDiffsFlushed, "train diffs flushed once per tick, ahead of the belt");
            Assert.Greater(rig.RealBeltHashesReceived, 0, "real belt hashes verified");
            Assert.Greater(rig.DummyBeltHashesReceived, 0, "dummy belt hashes passed");
            Assert.IsFalse(rig.TickState.IsPermanentlyWaiting, "no stop");
            BeltFullStateAssert.AreEqual(CaptureServer(), rig.Cache.Replica.CaptureFullState());
        }

        // 指定回の直前に行き止まりを延ばし、その次のtickを再構築tickとして記録する
        // Extend the dead end right before the chosen iterations and record the following tick as the expected rebuilt tick
        private static void PlaceOnPlacementIteration(int iteration, TrainUpdateService trainUpdateService, List<uint> expectedRebuiltTicks)
        {
            if (iteration != FirstPlacementIteration && iteration != SecondPlacementIteration) return;
            ExtendDeadEnd(iteration == FirstPlacementIteration ? 22 : 23);
            expectedRebuiltTicks.Add(trainUpdateService.GetCurrentTick() + 1);
        }
    }
}
