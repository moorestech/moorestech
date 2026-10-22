using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Network;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.FluidNetwork;
using Client.Game.InGame.FluidNetwork.Network;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Tests.Inventory;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Fluid.Sync.Message;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Event.EventReceive.FluidNetworkSync;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // クライアントのtick同期一式(tick状態・順序待ちキュー・ベルトと流体の複製・受信口・本物の検証器)を組み、サーバーのイベントをシミュレータと同じ順で流す
    // Assembles the client tick sync set (tick state, ordered buffer, belt and fluid replicas, receive ports, the real verifier) and flushes server events in the simulator's order
    // 列車の全量ハンドラと束ハンドラの役はここが代行する: 水位でtick状態を初期化し、束の差分を空の適用・ハッシュをキューへ積む
    // Plays the train snapshot and bundle handlers' parts: initializes the tick state at the watermark, queues the bundle's diff as an empty apply and its hashes
    internal sealed class TickSyncClientRig
    {
        // 登録pushの並び: 列車の全量・レール・ベルトの全量・流体の全量
        // Registration push order: train snapshot, rail, belt full state, fluid full state
        public const int BeltSnapshotPushIndex = 2;
        public const int FluidSnapshotPushIndex = 3;
        public const int RegistrationPushCount = 4;

        public readonly TrainUnitTickState TickState = new();
        public readonly TrainUnitFutureMessageBuffer FutureMessageBuffer;
        public readonly BeltTransportClientCache Cache = new(new BeltSegmentExitStatisticsTable());
        public readonly FluidNetworkClientCache FluidCache = new();
        public readonly CapturingVanillaApiEvent ApiEvent = new();
        public readonly BeltTransportEventNetworkHandler Handler;
        public readonly FluidNetworkEventNetworkHandler FluidHandler;
        public readonly TrainUnitHashVerifier Verifier;
        public int TrainDiffsFlushed { get; private set; }
        public int RealBeltHashesReceived { get; private set; }
        public int DummyBeltHashesReceived { get; private set; }
        public int RebuiltFullStatesReceived { get; private set; }
        public int FluidRebuiltFullStatesReceived { get; private set; }

        public TickSyncClientRig()
        {
            var diagnostics = new TrainSynchronizationDiagnostics(TickState, new TrainSynchronizationDiagnosticWriter(Path.Combine(Application.temporaryCachePath, "TickSyncTest")));
            FutureMessageBuffer = new TrainUnitFutureMessageBuffer(TickState, diagnostics);
            Handler = new BeltTransportEventNetworkHandler(ApiEvent, Cache, FutureMessageBuffer, TickState, diagnostics);
            Handler.Initialize();
            FluidHandler = new FluidNetworkEventNetworkHandler(ApiEvent, FluidCache, FutureMessageBuffer, TickState, diagnostics);
            FluidHandler.Initialize();
            var railGraphCache = RailGraphClientCache.CreateForEditorTest();
            Verifier = new TrainUnitHashVerifier(FutureMessageBuffer, new TrainUnitClientCache(railGraphCache), railGraphCache, Cache, FluidCache, TickState, diagnostics);
        }

        // 登録pushの3件目がベルト、4件目が流体の全量。ベルトの水位でtick状態を初期化し、両方の全量を受信口へ渡す
        // The third registration push is the belt full state and the fourth the fluid one; initialize the tick state at the belt watermark and hand both to the receive ports
        public void Join(List<EventMessagePack> registrationPushes)
        {
            Assert.AreEqual(RegistrationPushCount, registrationPushes.Count, "registration pushes");
            Assert.AreEqual(BeltTransportFullSnapshotEventPacket.EventTag, registrationPushes[BeltSnapshotPushIndex].Tag);
            Assert.AreEqual(FluidNetworkFullSnapshotEventPacket.EventTag, registrationPushes[FluidSnapshotPushIndex].Tag);
            var snapshot = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(registrationPushes[BeltSnapshotPushIndex].Payload);
            TickState.Initialize(TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.ServerTick, snapshot.TickSequenceId));
            ApiEvent.Dispatch(registrationPushes[BeltSnapshotPushIndex].Tag, registrationPushes[BeltSnapshotPushIndex].Payload);
            ApiEvent.Dispatch(registrationPushes[FluidSnapshotPushIndex].Tag, registrationPushes[FluidSnapshotPushIndex].Payload);
            Assert.AreEqual(UniTaskStatus.Succeeded, Handler.WaitForInitialApplyAsync().Status, "belt initial apply completed");
            Assert.AreEqual(UniTaskStatus.Succeeded, FluidHandler.WaitForInitialApplyAsync().Status, "fluid initial apply completed");
        }

        public void Receive(EventMessagePack eventMessagePack)
        {
            if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
            {
                ReceiveTrainBundle(MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload));
                return;
            }
            if (eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag) RebuiltFullStatesReceived++;
            if (eventMessagePack.Tag == FluidNetworkTickEventPacket.RebuiltFullStateEventTag) FluidRebuiltFullStatesReceived++;
            // ブロック状態など同期外のイベントはこのリグに受け口が無いので流さない
            // Events outside the tick sync (block states etc.) have no receiver in this rig and are not dispatched
            if (!IsTickSyncEvent(eventMessagePack.Tag)) return;
            ApiEvent.Dispatch(eventMessagePack.Tag, eventMessagePack.Payload);
        }

        private static bool IsTickSyncEvent(string tag)
        {
            return tag == BeltTransportTickEventPacket.TickDiffEventTag || tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag
                || tag == FluidNetworkTickEventPacket.TickDiffEventTag || tag == FluidNetworkTickEventPacket.RebuiltFullStateEventTag;
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
            FutureMessageBuffer.EnqueueHash(bundle.UnitsHash, bundle.RailGraphHash, bundle.BeltTransportHash, bundle.FluidNetworkHash, hashTick, bundle.HashTickSequenceId);
            FutureMessageBuffer.EnqueueEvent(bundle.ServerTick, bundle.DiffTickSequenceId, TrainTickBufferedEvent.Create(() => TrainDiffsFlushed++));
            if (TrainUpdateService.IsHashBroadcastTick(hashTick)) RealBeltHashesReceived++;
            else DummyBeltHashesReceived++;
        }
    }
}
