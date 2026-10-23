using System.Collections.Generic;
using Client.Tests.BeltTransport;
using Core.Update;
using Game.Fluid.Sync.Diff;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot.Loop;
using Server.Event;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.UnitTest.Game.Fluid.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync.FluidSyncPacketTestUtil;

namespace Client.Tests.FluidNetwork
{
    // サーバーが送った途中参加の全量・毎tickの差分・再構築の全量を、クライアントの受信口から列車の順序待ちキューへ通して流し、流体の複製がサーバーと一致し、検証器が列車束の流体ハッシュを通すか
    // Whether the server's join-time full state, per-tick diffs and rebuilt full states, fed through the client receive port into the train's ordered buffer, keep the fluid replica equal to the server and pass the verifier against the fluid hash on the train bundle
    public class FluidNetworkEventNetworkHandlerTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void ReplicaFedThroughTheOrderedBufferMatchesServerAndPassesTheVerifier()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var actionQueue = serviceProvider.GetService<ServerThreadActionQueue>();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);

            var rig = new TickSyncClientRig();
            rig.Join(sink.TakeAll());
            Assert.AreEqual(ServerHash(), rig.FluidCache.ComputeCurrentHash(), "replica hash right after the join-time full state");

            var expectedRebuiltTicks = new List<uint>();
            for (var i = 1; i <= TickCount; i++)
            {
                if (i == FirstPlacementIteration || i == SecondPlacementIteration)
                {
                    Assert.IsTrue(actionQueue.TryEnqueue(new ExtendPipeRunAtTickEnd(i == FirstPlacementIteration ? PipeRunLength + 1 : PipeRunLength + 2)), "placement queued for the tick end");
                    expectedRebuiltTicks.Add(trainUpdateService.GetCurrentTick());
                }
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) rig.Receive(eventMessagePack);
                rig.FlushLikeTheSimulator();

                // 毎tick、前tickのハッシュを検証器が通し、流体の連番まで流れた複製はサーバーと同じ状態
                // Every tick the verifier passes the previous tick's hash, and once flushed up to the fluid's sequence id the replica equals the server
                var tick = (trainUpdateService.GetCurrentTick() - 1);
                Assert.AreEqual(tick, rig.TickState.GetTick(), $"client reached tick {tick}");
                Assert.AreEqual(ServerHash(), rig.FluidCache.ComputeCurrentHash(), $"replica hash at tick {tick}");
            }

            Assert.AreEqual(expectedRebuiltTicks.Count, rig.FluidRebuiltFullStatesReceived, "one rebuilt full state per placement");
            Assert.AreEqual(TickCount, rig.TrainDiffsFlushed, "train diffs flushed once per tick");
            Assert.IsFalse(rig.TickState.IsPermanentlyWaiting, "no stop");
            FluidFullStateAssert.AreEqual(CaptureServer(), rig.FluidCache.Replica);
        }

        [Test]
        public void ReplicaOneTickAheadFailsTheNextRealHash()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var rig = new TickSyncClientRig();
            rig.Join(sink.TakeAll());

            // 本物のハッシュを持つtickまで進め、そのハッシュが届く前に流体の複製だけを1tick余分に進める
            // Advance to a tick that carries a real hash and, before that hash arrives, push the fluid replica alone one extra tick
            while (!TrainUpdateService.IsHashBroadcastTick(rig.TickState.GetTick())) AdvanceServerOneTick();
            var stoppedAtTick = rig.TickState.GetTick();
            Assert.IsTrue(rig.FluidCache.ApplyTickDiff(FluidTickDiff.Empty), "the extra tick itself is consistent");

            // 次tickに届く流体ハッシュで不一致になり、列車tickは止まる
            // The fluid hash arriving next tick mismatches and the train tick stops advancing
            AdvanceServerOneTick();
            AdvanceServerOneTick();
            Assert.AreEqual(stoppedAtTick, rig.TickState.GetTick(), "client tick no longer advances");

            #region Internal

            void AdvanceServerOneTick()
            {
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) rig.Receive(eventMessagePack);
                rig.FlushLikeTheSimulator();
            }

            #endregion
        }
    }
}
