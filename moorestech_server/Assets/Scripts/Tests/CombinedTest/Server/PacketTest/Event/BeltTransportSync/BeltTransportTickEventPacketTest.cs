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
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using Tests.UnitTest.Game.BeltConnection.Sync;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync
{
    // 搬送tickごとのベルトイベントが、毎tick1件・連番の欠番なし・再構築tickは全量で送られ、イベントだけで進む複製がサーバーと列車束のハッシュに一致するか
    // Whether per-tick belt events go out once per tick with gapless sequence ids and a full state on rebuilt ticks, and a replica driven only by events matches the server and the train bundle hashes
    public class BeltTransportTickEventPacketTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        // 8tick暖機後、24回目の前の設置は再構築tick32(ハッシュtick)に、41回目の前の設置はハッシュtick48の直後に当たる
        // After an 8-tick warm-up, the placement before iteration 24 rebuilds at tick 32 (a hash tick) and the one before iteration 41 lands right after hash tick 48
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void EveryTickSendsOneBeltEventWithGaplessSequenceIdsAndRebuiltFullState()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var sequenceIdsByTick = new Dictionary<uint, List<uint>>();
            var expectedRebuiltTicks = new List<uint>();
            var rebuiltTicks = new List<uint>();
            var emptyDiffs = 0;
            var nonEmptyDiffs = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                var tick = trainUpdateService.GetCurrentTick();
                var events = sink.TakeAll();
                CollectSequenceIds(events, sequenceIdsByTick);

                // 毎tickちょうど1件、そのtickの番号で届く
                // Exactly one belt event per tick, stamped with that tick
                var beltEvents = events.Where(IsBeltTickEvent).ToList();
                Assert.AreEqual(1, beltEvents.Count, $"belt events at tick {tick}");
                if (beltEvents[0].Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag)
                {
                    var full = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(beltEvents[0].Payload);
                    Assert.AreEqual(tick, full.ServerTick, "rebuilt full state tick");
                    BeltFullStateAssert.AreEqual(CaptureServer(), full.ToFullState());
                    rebuiltTicks.Add(tick);
                    continue;
                }
                var diff = MessagePackSerializer.Deserialize<BeltTransportTickDiffMessagePack>(beltEvents[0].Payload);
                Assert.AreEqual(tick, diff.ServerTick, "diff tick");
                if (diff.ToDiff().IsEmpty) emptyDiffs++;
                else nonEmptyDiffs++;
            }

            // 設置の次のtickだけが全量で、その後は差分に戻った。空の差分も送られている
            // Only the tick after each placement carried a full state, later ticks went back to diffs, and empty diffs were sent too
            CollectionAssert.AreEqual(expectedRebuiltTicks, rebuiltTicks, "rebuilt ticks");
            Assert.Greater(emptyDiffs, 0, "empty diffs are sent");
            Assert.Greater(nonEmptyDiffs, 0, "machine handoffs are sent");

            // 各tickは列車のdiff・ベルト・列車のhashの3つで、欠番も重複も無い
            // Each tick holds three ids (train diff, belt, train hash) with no gap or duplicate
            Assert.AreEqual(TickCount - 1, AssertNoSequenceGaps(sequenceIdsByTick, 3), "fully observed ticks");
        }

        [Test]
        public void ReplicaDrivenOnlyByEventsMatchesServerAndBundleBeltHashes()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            // 登録時のpushを捨てずに受け、3件目のベルト全量から複製を組む
            // Keep the registration push and assemble the replica from the third event, the belt full state
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var initial = sink.TakeAll();
            Assert.AreEqual(3, initial.Count, "initial pushes");
            Assert.AreEqual(BeltTransportFullSnapshotEventPacket.EventTag, initial[2].Tag);
            var snapshot = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(initial[2].Payload);
            var replica = BeltTransportReplicaAssembler.Assemble(snapshot.ToFullState());
            var replicaHashByTick = new Dictionary<uint, uint> { [snapshot.ServerTick] = HashOf(replica) };

            var expectedRebuiltTicks = new List<uint>();
            var rebuilds = 0;
            var realHashesChecked = 0;
            var dummyHashesChecked = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) Apply(eventMessagePack);

                var tick = trainUpdateService.GetCurrentTick();
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

        // 指定回の直前に行き止まりを延ばし、その次のtickを再構築tickとして記録する
        // Extend the dead end right before the chosen iterations and record the following tick as the expected rebuilt tick
        private static void PlaceOnPlacementIteration(int iteration, TrainUpdateService trainUpdateService, List<uint> expectedRebuiltTicks)
        {
            if (iteration != FirstPlacementIteration && iteration != SecondPlacementIteration) return;
            ExtendDeadEnd(iteration == FirstPlacementIteration ? 22 : 23);
            expectedRebuiltTicks.Add(trainUpdateService.GetCurrentTick() + 1);
        }

        private static uint HashOf(BeltTransportReplica replica)
        {
            return BeltTransportStateHash.Compute(replica.CaptureFullState());
        }
    }
}
