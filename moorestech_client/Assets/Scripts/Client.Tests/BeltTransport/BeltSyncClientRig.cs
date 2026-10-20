using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Network;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Tests.Inventory;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // クライアントのベルト同期一式(tick状態・順序待ちキュー・複製・受信口・本物の検証器)を組み、サーバーのイベントをシミュレータと同じ順で流す
    // Assembles the client belt sync set (tick state, ordered buffer, replica, receive port, the real verifier) and flushes server events in the simulator's order
    // 列車の全量ハンドラと束ハンドラの役はここが代行する: 水位でtick状態を初期化し、束の差分を空の適用・ハッシュをキューへ積む
    // Plays the train snapshot and bundle handlers' parts: initializes the tick state at the watermark, queues the bundle's diff as an empty apply and its hashes
    internal sealed class BeltSyncClientRig
    {
        public readonly TrainUnitTickState TickState = new();
        public readonly TrainUnitFutureMessageBuffer FutureMessageBuffer;
        public readonly BeltTransportClientCache Cache = new(new BeltSegmentExitStatisticsTable());
        public readonly CapturingVanillaApiEvent ApiEvent = new();
        public readonly BeltTransportEventNetworkHandler Handler;
        public readonly TrainUnitHashVerifier Verifier;
        public int TrainDiffsFlushed { get; private set; }
        public int RealBeltHashesReceived { get; private set; }
        public int DummyBeltHashesReceived { get; private set; }
        public int RebuiltFullStatesReceived { get; private set; }

        public BeltSyncClientRig()
        {
            var diagnostics = new TrainSynchronizationDiagnostics(TickState, new TrainSynchronizationDiagnosticWriter(Path.Combine(Application.temporaryCachePath, "BeltTransportSyncTest")));
            FutureMessageBuffer = new TrainUnitFutureMessageBuffer(TickState, diagnostics);
            Handler = new BeltTransportEventNetworkHandler(ApiEvent, Cache, FutureMessageBuffer, TickState, diagnostics);
            Handler.Initialize();
            var railGraphCache = RailGraphClientCache.CreateForEditorTest();
            Verifier = new TrainUnitHashVerifier(FutureMessageBuffer, new TrainUnitClientCache(railGraphCache), railGraphCache, Cache, TickState, diagnostics);
        }

        // 登録pushの3件目がベルトの全量。その水位でtick状態を初期化し、全量を受信口へ渡す
        // The third registration push is the belt full state; initialize the tick state at its watermark and hand it to the receive port
        public void Join(List<EventMessagePack> registrationPushes)
        {
            Assert.AreEqual(3, registrationPushes.Count, "registration pushes");
            Assert.AreEqual(BeltTransportFullSnapshotEventPacket.EventTag, registrationPushes[2].Tag);
            var snapshot = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(registrationPushes[2].Payload);
            TickState.Initialize(TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.ServerTick, snapshot.TickSequenceId));
            ApiEvent.Dispatch(registrationPushes[2].Tag, registrationPushes[2].Payload);
            Assert.AreEqual(UniTaskStatus.Succeeded, Handler.WaitForInitialApplyAsync().Status, "initial apply completed");
        }

        public void Receive(EventMessagePack eventMessagePack)
        {
            if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
            {
                ReceiveTrainBundle(MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload));
                return;
            }
            if (eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag) RebuiltFullStatesReceived++;
            ApiEvent.Dispatch(eventMessagePack.Tag, eventMessagePack.Payload);
        }

        // 順序待ちのイベントを連番どおりに流し、ハッシュの位置は検証器に通してtickを進める。TrainUnitClientSimulatorの1フレームと同じ
        // Flush the ordered events in sequence order and pass hash positions through the verifier to advance the tick; the same as one TrainUnitClientSimulator frame
        public void FlushLikeTheSimulator()
        {
            while (true)
            {
                var nextId = TickState.GetAppliedTickUnifiedId() + 1;
                if (FutureMessageBuffer.TryFlushEvent(nextId)) continue;
                if (!Verifier.CanAdvanceTick(nextId)) break;
                TickState.AdvanceTick();
            }
        }

        private void ReceiveTrainBundle(TrainUnitTickDiffBundleMessagePack bundle)
        {
            var hashTick = bundle.ServerTick - 1;
            FutureMessageBuffer.EnqueueHash(bundle.UnitsHash, bundle.RailGraphHash, bundle.BeltTransportHash, hashTick, bundle.HashTickSequenceId);
            FutureMessageBuffer.EnqueueEvent(bundle.ServerTick, bundle.DiffTickSequenceId, TrainTickBufferedEvent.Create(() => TrainDiffsFlushed++));
            if (TrainUpdateService.IsHashBroadcastTick(hashTick)) RealBeltHashesReceived++;
            else DummyBeltHashesReceived++;
        }
    }
}
