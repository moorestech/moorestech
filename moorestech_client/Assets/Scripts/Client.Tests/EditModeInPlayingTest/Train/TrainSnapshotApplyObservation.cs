using System;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Network.API;
using Core.Update.TickSynchronization;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;

namespace Client.Tests.EditModeInPlayingTest
{
    // raw受信と適用直後を別々に記録し、購読順に依存せず照合する。
    // Record raw receipt and immediate application independently of subscription order.
    internal sealed class TrainSnapshotApplyObservation : IDisposable
    {
        private readonly CompositeDisposable _subscriptions = new();
        private int _order;
        public int RailCount { get; private set; }
        public int TrainCount { get; private set; }
        public int AppliedCount { get; private set; }
        public int RailReceivedOrder { get; private set; }
        public int TrainAppliedOrder { get; private set; }
        public ulong RailWatermark { get; private set; }
        public ulong TrainWatermark { get; private set; }
        public ulong AppliedWatermark { get; private set; }
        public ulong StateAtApply { get; private set; }
        public uint RailPayloadHash { get; private set; }
        public uint TrainPayloadHash { get; private set; }
        public uint RailHashAtApply { get; private set; }
        public uint TrainHashAtApply { get; private set; }
        public ClientTrainUnit TrainUnitAtApply { get; private set; }
        public ClientRailNode RailNodeAtApply { get; private set; }
        public TrainCarEntityObject CarViewAtApply { get; private set; }
        public bool HasCompletePair => RailCount == 1 && TrainCount == 1 && AppliedCount == 1;
        public string Diagnostic => $"rail={RailCount}/{RailWatermark}, train={TrainCount}/{TrainWatermark}, applied={AppliedCount}/{AppliedWatermark}, state={StateAtApply}";

        public TrainSnapshotApplyObservation(IVanillaApiEvent events, TrainFullSnapshotEventNetworkHandler handler,
            TrainTickContext context, TrainUnitClientCache trains, RailGraphClientCache rails,
            TrainCarObjectDatastore views, TrainUnitInstanceId trainId, TrainCarInstanceId carId, int railNodeId)
        {
            events.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, payload =>
            {
                var snapshot = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload).Snapshot;
                RailCount++;
                RailReceivedOrder = ++_order;
                RailWatermark = TickUnifiedIdUtility.CreateTickUnifiedId(snapshot.GraphTick, snapshot.GraphTickSequenceId);
                RailPayloadHash = snapshot.GraphHash;
            }).AddTo(_subscriptions);
            events.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, payload =>
            {
                var snapshot = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(payload);
                TrainCount++;
                TrainWatermark = TickUnifiedIdUtility.CreateTickUnifiedId(snapshot.ServerTick, snapshot.WatermarkTickSequenceId);
                TrainPayloadHash = snapshot.UnitsHash;
            }).AddTo(_subscriptions);

            // tickが進む前にhashと参照を確保し、通知だけの偽陽性を防ぐ。
            // Capture hashes and references before another tick can hide a notification-only failure.
            handler.OnFullSnapshotApplied.Subscribe(watermark =>
            {
                AppliedCount++;
                TrainAppliedOrder = ++_order;
                AppliedWatermark = watermark;
                StateAtApply = context.State.GetAppliedTickUnifiedId();
                TrainHashAtApply = trains.ComputeCurrentHash();
                RailHashAtApply = rails.ComputeCurrentHash();
                TrainUnitAtApply = trains.Units[trainId];
                RailNodeAtApply = rails.Nodes[railNodeId];
                views.TryGetEntity(carId, out var view);
                CarViewAtApply = view;
            }).AddTo(_subscriptions);
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
