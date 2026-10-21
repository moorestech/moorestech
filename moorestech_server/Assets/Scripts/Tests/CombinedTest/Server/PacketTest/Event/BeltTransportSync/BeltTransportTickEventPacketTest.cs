using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot.Loop;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using Tests.UnitTest.Game.BeltConnection.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync
{
    // 搬送tickごとのベルト差分が毎tick1件・連番の欠番なしで送られ、tick末尾で作り直したtickは差分の後・ハッシュの前に全量が続き、イベントだけで進む複製がサーバーと列車束のハッシュに一致するか
    // Whether per-tick belt diffs go out once per tick with gapless sequence ids, a tick rebuilt at its end follows the diff with a full state before the hash, and a replica driven only by events matches the server and the train bundle hashes
    public class BeltTransportTickEventPacketTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        // 8tick暖機後、24回目のtick末尾の設置は再構築tick32(ハッシュtick)に、41回目はハッシュtick48の直後に当たる
        // After an 8-tick warm-up, the tick-end placement in iteration 24 rebuilds at tick 32 (a hash tick) and the one in iteration 41 lands right after hash tick 48
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void EveryTickSendsOneBeltDiffWithGaplessSequenceIdsAndRebuiltFullStateBeforeHash()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var actionQueue = serviceProvider.GetService<ServerThreadActionQueue>();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var sequenceIdsByTick = new Dictionary<uint, List<uint>>();
            var expectedRebuiltTicks = new List<uint>();
            var rebuiltTicks = new List<uint>();
            var emptyDiffs = 0;
            var nonEmptyDiffs = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, actionQueue, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                var tick = (trainUpdateService.GetCurrentTick() - 1);
                var events = sink.TakeAll();
                CollectSequenceIds(events, sequenceIdsByTick);

                // 毎tick先頭に差分が1件、そのtickの番号で届く。作り直したtickだけ、その後に作り直し後の全量が続く
                // Each tick starts with exactly one diff stamped with that tick; only a rebuilt tick follows it with the rebuilt full state
                var beltEvents = events.Where(IsBeltTickEvent).ToList();
                Assert.AreEqual(BeltTransportTickEventPacket.TickDiffEventTag, beltEvents[0].Tag, $"diff first at tick {tick}");
                if (beltEvents.Count == 2)
                {
                    Assert.AreEqual(BeltTransportTickEventPacket.RebuiltFullStateEventTag, beltEvents[1].Tag, $"full state after the diff at tick {tick}");
                    var full = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(beltEvents[1].Payload);
                    Assert.AreEqual(tick, full.ServerTick, "rebuilt full state tick");
                    BeltFullStateAssert.AreEqual(CaptureServer(), full.ToFullState());
                    rebuiltTicks.Add(tick);
                }
                else Assert.AreEqual(1, beltEvents.Count, $"belt events at tick {tick}");
                var diff = MessagePackSerializer.Deserialize<BeltTransportTickDiffMessagePack>(beltEvents[0].Payload);
                Assert.AreEqual(tick, diff.ServerTick, "diff tick");
                var decodedDiff = diff.ToDiff();
                if (decodedDiff.Inserts.Length + decodedDiff.Extracts.Length == 0) emptyDiffs++;
                else nonEmptyDiffs++;
            }

            // 設置したtickだけが全量を伴い、その後は差分だけに戻った。空の差分も送られている
            // Only each placing tick carried a full state, later ticks went back to diffs alone, and empty diffs were sent too
            CollectionAssert.AreEqual(expectedRebuiltTicks, rebuiltTicks, "rebuilt ticks");
            Assert.Greater(emptyDiffs, 0, "empty diffs are sent");
            Assert.Greater(nonEmptyDiffs, 0, "machine handoffs are sent");

            // 各tickは列車のdiff・ベルト差分・列車のhashの3つ、作り直したtickは全量を足した4つで、欠番も重複も無い
            // Each tick holds three ids (train diff, belt diff, train hash), a rebuilt tick four with the full state, with no gap or duplicate
            Assert.AreEqual(TickCount - 1, AssertNoSequenceGaps(sequenceIdsByTick, rebuiltTicks), "fully observed ticks");
        }

        [Test]
        public void ReplicaDrivenOnlyByEventsMatchesServerAndBundleBeltHashes()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var actionQueue = serviceProvider.GetService<ServerThreadActionQueue>();

            // 登録時のpushを捨てずに受け、3件目のベルト全量から複製を組む
            // Keep the registration push and assemble the replica from the third event, the belt full state
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var initial = sink.TakeAll();
            Assert.AreEqual(3, initial.Count, "initial pushes");
            Assert.AreEqual(BeltTransportFullSnapshotEventPacket.EventTag, initial[2].Tag);
            var snapshot = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(initial[2].Payload);
            var replica = BeltTransportReplicaAssembler.Assemble(snapshot.ToFullState());
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
                Assert.AreEqual(BeltTransportStateHash.Compute(CaptureServer()), replicaHashByTick[tick], $"replica hash at tick {tick}");
            }

            Assert.AreEqual(expectedRebuiltTicks.Count, rebuilds, "replica re-assembled once per placement");
            Assert.Greater(realHashesChecked, 0, "real belt hashes compared");
            Assert.Greater(dummyHashesChecked, 0, "dummy belt hashes compared");
            BeltFullStateAssert.AreEqual(CaptureServer(), replica.CaptureFullState());

            #region Internal

            void Apply(EventMessagePack eventMessagePack)
            {
                if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag) CheckBundleHash(MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload));
                else if (eventMessagePack.Tag == BeltTransportTickEventPacket.TickDiffEventTag) ApplyDiff(MessagePackSerializer.Deserialize<BeltTransportTickDiffMessagePack>(eventMessagePack.Payload));
                else if (eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag) Reassemble(MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(eventMessagePack.Payload));
            }

            // 束はtick n-1のベルトハッシュを載せる。間引きtickはダミー、本物は複製がtick n-1を処理した直後のハッシュと一致する
            // A bundle carries the belt hash of tick n-1; a skipped tick is the dummy, a real one equals the replica hash right after it processed tick n-1
            void CheckBundleHash(TrainUnitTickDiffBundleMessagePack bundle)
            {
                var hashTick = bundle.ServerTick - 1;
                if (!TrainUpdateService.IsHashBroadcastTick(hashTick))
                {
                    Assert.AreEqual(uint.MaxValue, bundle.BeltTransportHash, $"dummy belt hash for tick {hashTick}");
                    dummyHashesChecked++;
                    return;
                }
                Assert.IsTrue(replicaHashByTick.ContainsKey(hashTick), $"replica processed tick {hashTick} before its hash bundle");
                Assert.AreEqual(replicaHashByTick[hashTick], bundle.BeltTransportHash, $"belt hash for tick {hashTick}");
                realHashesChecked++;
            }

            void ApplyDiff(BeltTransportTickDiffMessagePack message)
            {
                Assert.IsTrue(replica.Tick(message.ToDiff()), $"replica consistent at tick {message.ServerTick}");
                replicaHashByTick[message.ServerTick] = HashOf(replica);
            }

            void Reassemble(BeltTransportFullStateMessagePack message)
            {
                replica = BeltTransportReplicaAssembler.Assemble(message.ToFullState());
                replicaHashByTick[message.ServerTick] = HashOf(replica);
                rebuilds++;
            }

            #endregion
        }

        // 指定回では、これから回るtickの末尾で行き止まりを延ばすよう積み、そのtickを再構築tickとして記録する。クライアント操作と同じくtick末尾で適用される
        // On the chosen iterations, queue a dead-end extension for the end of the tick about to run and record that tick as the expected rebuilt one; it applies at the tick end like client input
        private static void PlaceOnPlacementIteration(int iteration, TrainUpdateService trainUpdateService, ServerThreadActionQueue actionQueue, List<uint> expectedRebuiltTicks)
        {
            if (iteration != FirstPlacementIteration && iteration != SecondPlacementIteration) return;
            Assert.IsTrue(actionQueue.TryEnqueue(new ExtendDeadEndAtTickEnd(iteration == FirstPlacementIteration ? 22 : 23)), "placement queued for the tick end");
            expectedRebuiltTicks.Add(trainUpdateService.GetCurrentTick());
        }

        private sealed class ExtendDeadEndAtTickEnd : IServerThreadAction
        {
            private readonly int _x;

            public ExtendDeadEndAtTickEnd(int x)
            {
                _x = x;
            }

            public void Run()
            {
                ExtendDeadEnd(_x);
            }

            public void OnServerStopped()
            {
            }
        }

        private static uint HashOf(BeltTransportReplica replica)
        {
            return BeltTransportStateHash.Compute(replica.CaptureFullState());
        }
    }
}
