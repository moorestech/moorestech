using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Core.Update.TickSynchronization;

namespace Client.Game.InGame.Train.Network
{
    // full snapshotイベントをストリーム到着順に即時適用する唯一のsnapshot適用経路
    // The single snapshot-apply path: applies full snapshots immediately in stream arrival order
    public sealed class TrainFullSnapshotEventNetworkHandler : IInitializable, IDisposable, IInitialEventApplyWaitTarget
    {
        private readonly RailGraphSnapshotApplier _railGraphSnapshotApplier;
        private readonly TrainUnitSnapshotApplier _trainSnapshotApplier;
        private readonly TrainTickContext _context;
        private ulong? _railWatermark;
        private IDisposable _railSubscription;
        private IDisposable _trainSubscription;

        private readonly UniTaskCompletionSource _initialApplyCompletion = new();

        public UniTask WaitForInitialApplyAsync()
        {
            return _initialApplyCompletion.Task;
        }

        public TrainFullSnapshotEventNetworkHandler(
            RailGraphSnapshotApplier railGraphSnapshotApplier,
            TrainUnitSnapshotApplier trainSnapshotApplier,
            TrainTickContext context)
        {
            _railGraphSnapshotApplier = railGraphSnapshotApplier;
            _trainSnapshotApplier = trainSnapshotApplier;
            _context = context;
        }

        public void Initialize()
        {
            var vanillaApiEvent = ClientContext.VanillaApi.Event;
            _railSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, HandleRailGraphFullSnapshot);
            _trainSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, HandleTrainUnitFullSnapshot);
        }

        // ネットワーク受信payloadのデシリアライズと適用を隔離する外部境界。畳まないと完了ソースがPendingで残りWhenAllが無期限待機に化ける
        // External boundary isolating deserialization and apply of a received network payload; without folding, the source stays Pending and WhenAll hangs
        private void HandleRailGraphFullSnapshot(byte[] payload)
        {
            if (_context.State.IsStopped || _context.IsInitialSnapshotApplied) return;
            try
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload);
                if (_railWatermark.HasValue) throw new InvalidOperationException("Duplicate initial rail snapshot.");
                _railGraphSnapshotApplier.ApplySnapshot(message.Snapshot);
                _railWatermark = TickUnifiedIdUtility.CreateTickUnifiedId(message.Snapshot.GraphTick, message.Snapshot.GraphTickSequenceId);
            }
            catch (Exception applyException)
            {
                FailInitialApply("railGraph", applyException);
            }
        }

        // レール側と同じくネットワークpayloadを隔離する外部境界
        // The same external boundary as the rail side, isolating a received network payload
        private void HandleTrainUnitFullSnapshot(byte[] payload)
        {
            if (_context.State.IsStopped || _context.IsInitialSnapshotApplied) return;
            try
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(payload);

                // 初期pairは同じ採番位置で両payloadが揃っていることを要求する
                // Require both initial payloads to represent the same sequence watermark
                var watermarkId = TickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.WatermarkTickSequenceId);
                if (!_railWatermark.HasValue || _railWatermark.Value != watermarkId)
                    throw new InvalidOperationException($"Incoherent initial snapshot watermark: rail={_railWatermark}, train={watermarkId}");
                if (message.Snapshots == null) throw new InvalidOperationException("Initial train snapshot list is missing.");
                var bundles = new List<TrainUnitSnapshotBundle>(message.Snapshots.Count);
                foreach (var snapshot in message.Snapshots) bundles.Add(snapshot.ToModel());

                var response = new TrainUnitSnapshotResponse(bundles, message.ServerTick, message.UnitsHash, message.WatermarkTickSequenceId);
                _trainSnapshotApplier.ApplySnapshot(response);

                // watermark以下の古いdiff/hashをpurgeし、以後のイベントが連続適用できる状態にする
                // Purge stale diffs/hashes at or below the watermark so later events continue seamlessly
                _context.Events.DiscardEventsAtOrBelow(watermarkId);
                _context.Hashes.DiscardHashesOlderThan(watermarkId);

                // 両cacheとviewの成功が確定してから起動を解放する
                // Release startup only after both caches and views have succeeded
                _context.CompleteInitialSnapshot();
                _initialApplyCompletion.TrySetResult();
            }
            catch (Exception applyException)
            {
                FailInitialApply("trainUnit", applyException);
            }
        }

        private void FailInitialApply(string domain, Exception exception)
        {
            // 待機側の再入より先に保存なし終了を確定する
            // Establish no-save exit before faulting the wait can re-enter initialization
            var failure = new TrainInitialSnapshotException($"[TrainFullSnapshot] {domain} initial apply failed: {exception.Message}", exception);
            _context.State.Stop(failure.ToString());
            GameShutdownEvent.QuitAfterSynchronizationFailure();
            _initialApplyCompletion.TrySetException(failure);
        }

        public void Dispose()
        {
            _railSubscription?.Dispose();
            _trainSubscription?.Dispose();
        }
    }
}
