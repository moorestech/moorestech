using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Fluid.Sync.Message;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot.Loop;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.FluidNetworkSync;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync;
using Tests.UnitTest.Game.Fluid.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync.FluidSyncPacketTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync
{
    // 流体の差分が毎tick1件・連番の欠番なしで送られ、tick末尾で作り直したtickは差分の後・ハッシュの前に全量が続き、イベントだけで進む複製がサーバーと列車束のハッシュに一致するか
    // Whether per-tick fluid diffs go out once per tick with gapless sequence ids, a tick rebuilt at its end follows the diff with a full state before the hash, and a replica driven only by events matches the server and the train bundle hashes
    public class FluidNetworkTickEventPacketTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        // 8tick暖機後、24回目のtick末尾の設置は再構築tick32(ハッシュtick)に、41回目はハッシュtick48の直後に当たる
        // After an 8-tick warm-up, the tick-end placement in iteration 24 rebuilds at tick 32 (a hash tick) and the one in iteration 41 lands right after hash tick 48
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void EveryTickSendsOneFluidDiffWithGaplessSequenceIdsAndRebuiltFullStateBeforeHash()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var actionQueue = serviceProvider.GetService<ServerThreadActionQueue>();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var sequenceIdsByTick = new Dictionary<uint, List<uint>>();
            var expectedRebuiltTicks = new List<uint>();
            var rebuiltTicks = new List<uint>();
            var diffsWithDeliveries = 0;
            var diffsWithInflows = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, actionQueue, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                var tick = (trainUpdateService.GetCurrentTick() - 1);
                var events = sink.TakeAll();
                BeltSyncPacketTestUtil.CollectSequenceIds(events, sequenceIdsByTick);

                // 毎tick差分が1件、そのtickの番号で届く。作り直したtickだけ、その後に作り直し後の全量が続く
                // Each tick carries exactly one diff stamped with that tick; only a rebuilt tick follows it with the rebuilt full state
                var fluidEvents = events.Where(IsFluidTickEvent).ToList();
                Assert.AreEqual(FluidNetworkTickEventPacket.TickDiffEventTag, fluidEvents[0].Tag, $"diff first at tick {tick}");
                if (fluidEvents.Count == 2)
                {
                    Assert.AreEqual(FluidNetworkTickEventPacket.RebuiltFullStateEventTag, fluidEvents[1].Tag, $"full state after the diff at tick {tick}");
                    var full = MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(fluidEvents[1].Payload);
                    Assert.AreEqual(tick, full.ServerTick, "rebuilt full state tick");
                    FluidFullStateAssert.AreEqual(CaptureServer(), full.ToFullState());
                    rebuiltTicks.Add(tick);
                }
                else Assert.AreEqual(1, fluidEvents.Count, $"fluid events at tick {tick}");
                var diff = MessagePackSerializer.Deserialize<FluidNetworkTickDiffMessagePack>(fluidEvents[0].Payload).ToDiff();
                Assert.AreEqual(tick, MessagePackSerializer.Deserialize<FluidNetworkTickDiffMessagePack>(fluidEvents[0].Payload).ServerTick, "diff tick");
                if (0 < diff.Deliveries.Length) diffsWithDeliveries++;
                if (0 < diff.Inflows.Length) diffsWithInflows++;
            }

            // 設置したtickだけが全量を伴い、ボイドへの渡しとポンプからの流入が差分に載っている
            // Only each placing tick carried a full state, and the diffs carry both the deliveries into the void and the inflows from the pump
            CollectionAssert.AreEqual(expectedRebuiltTicks, rebuiltTicks, "rebuilt ticks");
            Assert.Greater(diffsWithDeliveries, 0, "deliveries into the void are sent");
            Assert.Greater(diffsWithInflows, 0, "inflows from the pump are sent");

            // 各tickは列車のdiff・ベルト差分・流体差分・列車のhashの4つ。パイプの設置はベルトの組を汚さないので、作り直したtickは流体の全量だけを足した5つ
            // Each tick holds four ids (train diff, belt diff, fluid diff, train hash); placing a pipe does not dirty the belt assembly, so a rebuilt tick holds five with only the fluid full state
            Assert.AreEqual(TickCount - 1, BeltSyncPacketTestUtil.AssertNoSequenceGaps(sequenceIdsByTick, rebuiltTicks, 1), "fully observed ticks");
        }

        [Test]
        public void ReplicaDrivenOnlyByEventsMatchesServerAndBundleFluidHashes()
        {
            var serviceProvider = CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var actionQueue = serviceProvider.GetService<ServerThreadActionQueue>();

            // 登録時のpushを捨てずに受け、4件目の流体全量から複製を組む
            // Keep the registration push and assemble the replica from the fourth event, the fluid full state
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var initial = sink.TakeAll();
            Assert.AreEqual(4, initial.Count, "initial pushes");
            Assert.AreEqual(FluidNetworkFullSnapshotEventPacket.EventTag, initial[3].Tag);
            var snapshot = MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(initial[3].Payload);
            var replica = FluidNetworkReplicaAssembler.Assemble(snapshot.ToFullState());
            // 登録時の全量は直前のtickを終えた状態。水位のtickは次に回るtickなので、複製のハッシュはその1つ前に対応する
            // The registration full state is the state after the previous tick; its watermark tick is the one about to run, so the replica hash belongs to the tick before
            var replicaHashByTick = new Dictionary<uint, uint> { [snapshot.ServerTick - 1] = HashOf(replica) };

            var expectedRebuiltTicks = new List<uint>();
            var rebuilds = 0;
            var realHashesChecked = 0;
            var dummyHashesChecked = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, actionQueue, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) Apply(eventMessagePack);

                var tick = (trainUpdateService.GetCurrentTick() - 1);
                Assert.IsTrue(replicaHashByTick.ContainsKey(tick), $"replica advanced to tick {tick}");
                Assert.AreEqual(FluidNetworkStateHash.Compute(CaptureServer()), replicaHashByTick[tick], $"replica hash at tick {tick}");
            }

            Assert.AreEqual(expectedRebuiltTicks.Count, rebuilds, "replica re-assembled once per placement");
            Assert.Greater(realHashesChecked, 0, "real fluid hashes compared");
            Assert.Greater(dummyHashesChecked, 0, "dummy fluid hashes compared");
            FluidFullStateAssert.AreEqual(CaptureServer(), replica.CaptureFullState());

            #region Internal

            void Apply(EventMessagePack eventMessagePack)
            {
                if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag) CheckBundleHash(MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload));
                else if (eventMessagePack.Tag == FluidNetworkTickEventPacket.TickDiffEventTag) ApplyDiff(MessagePackSerializer.Deserialize<FluidNetworkTickDiffMessagePack>(eventMessagePack.Payload));
                else if (eventMessagePack.Tag == FluidNetworkTickEventPacket.RebuiltFullStateEventTag) Reassemble(MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(eventMessagePack.Payload));
            }

            // 束はtick n-1の流体ハッシュを載せる。間引きtickはダミー、本物は複製がtick n-1を処理した直後のハッシュと一致する
            // A bundle carries the fluid hash of tick n-1; a skipped tick is the dummy, a real one equals the replica hash right after it processed tick n-1
            void CheckBundleHash(TrainUnitTickDiffBundleMessagePack bundle)
            {
                var hashTick = bundle.ServerTick - 1;
                if (!TrainUpdateService.IsHashBroadcastTick(hashTick))
                {
                    Assert.AreEqual(uint.MaxValue, bundle.FluidNetworkHash, $"dummy fluid hash for tick {hashTick}");
                    dummyHashesChecked++;
                    return;
                }
                Assert.IsTrue(replicaHashByTick.ContainsKey(hashTick), $"replica processed tick {hashTick} before its hash bundle");
                Assert.AreEqual(replicaHashByTick[hashTick], bundle.FluidNetworkHash, $"fluid hash for tick {hashTick}");
                realHashesChecked++;
            }

            void ApplyDiff(FluidNetworkTickDiffMessagePack message)
            {
                Assert.IsTrue(replica.Tick(message.ToDiff()), $"replica consistent at tick {message.ServerTick}");
                replicaHashByTick[message.ServerTick] = HashOf(replica);
            }

            void Reassemble(FluidNetworkFullStateMessagePack message)
            {
                replica = FluidNetworkReplicaAssembler.Assemble(message.ToFullState());
                replicaHashByTick[message.ServerTick] = HashOf(replica);
                rebuilds++;
            }

            #endregion
        }

        private static void PlaceOnPlacementIteration(int iteration, TrainUpdateService trainUpdateService, ServerThreadActionQueue actionQueue, List<uint> expectedRebuiltTicks)
        {
            if (iteration != FirstPlacementIteration && iteration != SecondPlacementIteration) return;
            Assert.IsTrue(actionQueue.TryEnqueue(new ExtendPipeRunAtTickEnd(iteration == FirstPlacementIteration ? PipeRunLength + 1 : PipeRunLength + 2)), "placement queued for the tick end");
            expectedRebuiltTicks.Add(trainUpdateService.GetCurrentTick());
        }
    }
}
