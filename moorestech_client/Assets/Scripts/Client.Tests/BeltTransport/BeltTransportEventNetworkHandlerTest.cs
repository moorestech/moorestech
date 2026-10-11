using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Network;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using Client.Tests.Inventory;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.UnitTest.Game.BeltConnection.Sync;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Client.Tests.BeltTransport
{
    // サーバーが送った途中参加の全量・毎tickの差分・再構築の全量を、クライアントの受信口から列車の順序待ちキューへ通して流し、複製がサーバーと列車束のベルトハッシュに一致するか
    // Whether the server's join-time full state, per-tick diffs and rebuilt full states, fed through the client receive port into the train's ordered buffer, keep the replica equal to the server and to the belt hash on the train bundle
    public class BeltTransportEventNetworkHandlerTest
    {
        private const int PlayerId = 1;
        private const int TickCount = 60;
        // 8tick暖機後、24回目の前の設置は再構築tick32(ハッシュtick)に、41回目の前の設置はハッシュtick48の直後に当たる
        // After an 8-tick warm-up, the placement before iteration 24 rebuilds at tick 32 (a hash tick) and the one before iteration 41 lands right after hash tick 48
        private const int FirstPlacementIteration = 24;
        private const int SecondPlacementIteration = 41;

        [Test]
        public void ReplicaFedThroughTheOrderedBufferMatchesServerAndBundleBeltHashes()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);

            var tickState = new TrainUnitTickState();
            var diagnostics = new TrainSynchronizationDiagnostics(tickState, new TrainSynchronizationDiagnosticWriter(Path.Combine(Application.temporaryCachePath, "BeltTransportSyncTest")));
            var futureMessageBuffer = new TrainUnitFutureMessageBuffer(tickState, diagnostics);
            var cache = new BeltTransportClientCache();
            var apiEvent = new CapturingVanillaApiEvent();
            var handler = new BeltTransportEventNetworkHandler(apiEvent, cache, futureMessageBuffer, tickState, diagnostics);
            handler.Initialize();

            // 登録pushの3件目がベルトの全量。列車の全量ハンドラの役(水位でtick状態を初期化)はテストが担う
            // The third registration push is the belt full state; the test plays the train snapshot handler's part (initializing the tick state at the watermark)
            var initial = sink.TakeAll();
            Assert.AreEqual(BeltTransportFullSnapshotEventPacket.EventTag, initial[2].Tag);
            var snapshot = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(initial[2].Payload);
            tickState.Initialize(TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.ServerTick, snapshot.TickSequenceId));
            apiEvent.Dispatch(initial[2].Tag, initial[2].Payload);
            Assert.AreEqual(UniTaskStatus.Succeeded, handler.WaitForInitialApplyAsync().Status, "initial apply completed");
            Assert.AreEqual(BeltTransportStateHash.Compute(CaptureServer()), cache.ComputeCurrentHash(), "replica hash right after the join-time full state");

            var beltHashByUnifiedId = new Dictionary<ulong, uint>();
            var expectedRebuiltTicks = new List<uint>();
            var rebuiltFullStates = 0;
            var trainDiffsFlushed = 0;
            var realHashesChecked = 0;
            var dummyHashesChecked = 0;
            for (var i = 1; i <= TickCount; i++)
            {
                PlaceOnPlacementIteration(i, trainUpdateService, expectedRebuiltTicks);
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) Receive(eventMessagePack);
                FlushLikeTheSimulator();

                // 毎tick、ベルトの連番まで流れた複製はサーバーと同じ状態
                // Every tick, once flushed up to the belt's sequence id the replica equals the server
                var tick = trainUpdateService.GetCurrentTick();
                Assert.AreEqual(tick, tickState.GetTick(), $"client reached tick {tick}");
                Assert.AreEqual(BeltTransportStateHash.Compute(CaptureServer()), cache.ComputeCurrentHash(), $"replica hash at tick {tick}");
            }

            Assert.AreEqual(expectedRebuiltTicks.Count, rebuiltFullStates, "one rebuilt full state per placement");
            Assert.AreEqual(TickCount, trainDiffsFlushed, "train diffs flushed once per tick, ahead of the belt");
            Assert.Greater(realHashesChecked, 0, "real belt hashes compared");
            Assert.Greater(dummyHashesChecked, 0, "dummy belt hashes compared");
            Assert.IsFalse(tickState.IsPermanentlyWaiting, "no stop");
            BeltFullStateAssert.AreEqual(CaptureServer(), cache.Replica.CaptureFullState());

            #region Internal

            // 列車の束は列車側ハンドラの役をテストが担い、差分を空の適用・ハッシュをキューへ積む。ベルトは本物の受信口へ渡す
            // The test plays the train bundle handler's part, queueing the diff as an empty apply and the hash; belt events go to the real receive port
            void Receive(EventMessagePack eventMessagePack)
            {
                if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
                {
                    var bundle = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload);
                    var hashTick = bundle.ServerTick - 1;
                    futureMessageBuffer.EnqueueHash(bundle.UnitsHash, bundle.RailGraphHash, hashTick, bundle.HashTickSequenceId);
                    beltHashByUnifiedId[TrainTickUnifiedIdUtility.CreateTickUnifiedId(hashTick, bundle.HashTickSequenceId)] = bundle.BeltTransportHash;
                    futureMessageBuffer.EnqueueEvent(bundle.ServerTick, bundle.DiffTickSequenceId, TrainTickBufferedEvent.Create(() => trainDiffsFlushed++));
                    return;
                }
                if (eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag) rebuiltFullStates++;
                apiEvent.Dispatch(eventMessagePack.Tag, eventMessagePack.Payload);
            }

            // 順序待ちのイベントを連番どおりに流し、ハッシュの位置では束のベルトハッシュを複製と照合してtickを進める
            // Flush the ordered events in sequence order; at a hash position compare the bundle's belt hash with the replica and advance the tick
            void FlushLikeTheSimulator()
            {
                while (true)
                {
                    var nextId = tickState.GetAppliedTickUnifiedId() + 1;
                    if (futureMessageBuffer.TryFlushEvent(nextId)) continue;
                    if (!futureMessageBuffer.TryDequeueHashAtTickSequenceId(nextId, out var hashState)) break;

                    var beltHash = beltHashByUnifiedId[nextId];
                    if (TrainUpdateService.IsHashBroadcastTick(hashState.serverTick))
                    {
                        Assert.AreEqual(beltHash, cache.ComputeCurrentHash(), $"belt hash for tick {hashState.serverTick}");
                        realHashesChecked++;
                    }
                    else
                    {
                        Assert.AreEqual(uint.MaxValue, beltHash, $"dummy belt hash for tick {hashState.serverTick}");
                        dummyHashesChecked++;
                    }
                    tickState.RecordAppliedTickUnifiedId(nextId);
                    tickState.AdvanceTick();
                }
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
    }
}
