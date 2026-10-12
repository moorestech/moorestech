using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.BeltTransport;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive.BeltTransportSync;
using Tests.CombinedTest.Server.PacketTest.Event;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync.BeltSyncPacketTestUtil;

namespace Client.Tests.BeltTransport
{
    // わざと複製をサーバーからずらし、検証器のベルトハッシュ不一致と、差分が複製に載らない食い違いのどちらでもクライアントが恒久停止するか
    // Deliberately drifts the replica from the server and checks the client stops permanently on both a verifier belt-hash mismatch and a diff that does not fit the replica
    public class BeltTransportMismatchStopsClientTest
    {
        private const int PlayerId = 1;
        // 検証器は受信済みtickが適用tickから200以上離れた時点で停止を確定する
        // The verifier confirms the stop once the received tick is 200 or more ahead of the applied tick
        private const int ReceivedGapForPermanentStop = 200;

        [Test]
        public void ReplicaOneTickAheadFailsTheNextRealHashAndStopsOnceTheReceivedGapGrows()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var rig = new BeltSyncClientRig();
            rig.Join(sink.TakeAll());

            // 本物のハッシュを送るtickまで進め、そのハッシュが届く前に複製だけを1tick余分に進める
            // Advance to a tick that carries a real hash and, before that hash arrives, push the replica alone one extra tick
            while (!TrainUpdateService.IsHashBroadcastTick(trainUpdateService.GetCurrentTick())) AdvanceServerOneTick();
            var stoppedAtTick = trainUpdateService.GetCurrentTick();
            Assert.IsTrue(rig.Cache.ApplyTickDiff(BeltTickDiff.Empty), "the extra tick itself is consistent");

            // 次tickに届くハッシュで不一致になり、列車tickは止まるが停止はまだ確定しない
            // The hash arriving next tick mismatches: the train tick stops advancing, but the stop is not confirmed yet
            AdvanceServerOneTick();
            Assert.AreEqual(stoppedAtTick, rig.TickState.GetTick(), "client tick no longer advances");
            Assert.IsFalse(rig.TickState.IsPermanentlyWaiting, "not yet permanent");

            // 受信が200tick先まで進むと恒久停止になり、以後も適用tickは動かない
            // Once reception runs 200 ticks ahead the stop becomes permanent and the applied tick never moves again
            for (var i = 0; i < ReceivedGapForPermanentStop + 8 && !rig.TickState.IsPermanentlyWaiting; i++) AdvanceServerOneTick();
            Assert.IsTrue(rig.TickState.IsPermanentlyWaiting, "stop confirmed");
            Assert.GreaterOrEqual(trainUpdateService.GetCurrentTick() - stoppedAtTick, ReceivedGapForPermanentStop, "stop confirmed by the received gap");
            AdvanceServerOneTick();
            Assert.AreEqual(stoppedAtTick, rig.TickState.GetTick(), "client tick stays at the mismatch");

            #region Internal

            void AdvanceServerOneTick()
            {
                GameUpdater.UpdateOneTick();
                foreach (var eventMessagePack in sink.TakeAll()) rig.Receive(eventMessagePack);
                rig.FlushLikeTheSimulator();
            }

            #endregion
        }

        [Test]
        public void ForgedExtractThatTheReplicaCannotHandStopsImmediately()
        {
            var serviceProvider = CreateServer();
            BuildMachineFedWorldAndWarmUp();
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(PlayerId, sink);
            var rig = new BeltSyncClientRig();
            rig.Join(sink.TakeAll());

            // 本物の差分に無い機械への搬出を1つ足した差分で、同じ連番位置を上書きする。複製はその搬出を再現できず食い違いになる
            // Overwrite the same sequence position with the real diff plus one machine extract the server did not report; the replica cannot reproduce it and diverges
            for (var i = 0; i < 20; i++)
            {
                GameUpdater.UpdateOneTick();
                var events = sink.TakeAll();
                foreach (var eventMessagePack in events) rig.Receive(eventMessagePack);
                var forged = TryForgeDiff(events);
                if (forged != null)
                {
                    LogAssert.Expect(LogType.Error, new Regex("replica did not hand an item"));
                    rig.ApiEvent.Dispatch(BeltTransportTickEventPacket.TickDiffEventTag, MessagePackSerializer.Serialize(forged));
                }
                rig.FlushLikeTheSimulator();
                if (forged == null) continue;

                Assert.IsTrue(rig.TickState.IsPermanentlyWaiting, "replica divergence stops the client");
                Assert.AreEqual(trainUpdateService.GetCurrentTick(), rig.TickState.GetTick(), "stopped on the forged tick");
                return;
            }
            Assert.Fail("no tick offered a machine output absent from the real diff");

            #region Internal

            BeltTransportTickDiffMessagePack TryForgeDiff(List<EventMessagePack> events)
            {
                var real = FindTickDiff(events);
                if (real == null) return null;
                var realDiff = real.ToDiff();
                if (!TryFindMachineOutputAbsentFrom(realDiff, out var forgedExtract)) return null;
                var extracts = new List<BeltMachineExtractRecord>(realDiff.Extracts) { forgedExtract };
                return new BeltTransportTickDiffMessagePack(real.ServerTick, real.TickSequenceId, new BeltTickDiff(realDiff.Inserts, extracts.ToArray()));
            }

            BeltTransportTickDiffMessagePack FindTickDiff(List<EventMessagePack> events)
            {
                foreach (var eventMessagePack in events)
                {
                    if (eventMessagePack.Tag == BeltTransportTickEventPacket.TickDiffEventTag) return MessagePackSerializer.Deserialize<BeltTransportTickDiffMessagePack>(eventMessagePack.Payload);
                }
                return null;
            }

            // 機械へ出る出力のうち、このtickの本物の差分に搬出が無いものを探す
            // Look for an output into a machine that the real diff of this tick reports no extract for
            bool TryFindMachineOutputAbsentFrom(BeltTickDiff realDiff, out BeltMachineExtractRecord forgedExtract)
            {
                var shapes = rig.Cache.Replica.Shapes;
                for (var segmentIndex = 0; segmentIndex < shapes.Length; segmentIndex++)
                {
                    foreach (var link in shapes[segmentIndex].Outputs)
                    {
                        if (!link.IsMachine || IsReported(segmentIndex, link.Direction)) continue;
                        forgedExtract = new BeltMachineExtractRecord(segmentIndex, link.Direction);
                        return true;
                    }
                }
                forgedExtract = default;
                return false;

                bool IsReported(int segmentIndex, BeltDirection direction)
                {
                    foreach (var extract in realDiff.Extracts)
                    {
                        if (extract.SegmentIndex == segmentIndex && extract.OutputDirection == direction) return true;
                    }
                    return false;
                }
            }

            #endregion
        }
    }
}
