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
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // full snapshotイベントをストリーム到着順に即時適用する唯一のsnapshot適用経路
    // The single snapshot-apply path: applies full snapshots immediately in stream arrival order
    public sealed class TrainFullSnapshotEventNetworkHandler : IInitializable, IDisposable, IInitialEventApplyWaitTarget
    {
        private readonly RailGraphSnapshotApplier _railGraphSnapshotApplier;
        private readonly TrainUnitSnapshotApplier _trainSnapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly Subject<ulong> _onFullSnapshotApplied = new();
        private IDisposable _railSubscription;
        private IDisposable _trainSubscription;

        // full snapshot適用完了通知（resyncゲート解除に使用）
        // Notifies full-snapshot application completion (used to release the resync gate)
        public IObservable<ulong> OnFullSnapshotApplied => _onFullSnapshotApplied;

        // スナップショット適用完了の通知口。イベント駆動でタスクを所有しないため完了ソースで表現する
        // Completion source signalling snapshot application; event-driven code owns no task of its own
        private readonly UniTaskCompletionSource _initialApplyCompletion = new();

        public UniTask WaitForInitialApplyAsync()
        {
            return _initialApplyCompletion.Task;
        }

        public TrainFullSnapshotEventNetworkHandler(
            RailGraphSnapshotApplier railGraphSnapshotApplier,
            TrainUnitSnapshotApplier trainSnapshotApplier,
            TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _railGraphSnapshotApplier = railGraphSnapshotApplier;
            _trainSnapshotApplier = trainSnapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
        }

        public void Initialize()
        {
            var vanillaApiEvent = ClientContext.VanillaApi.Event;
            _railSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, OnRailGraphFullSnapshot);
            _trainSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, OnTrainUnitFullSnapshot);

            #region Internal

            void OnRailGraphFullSnapshot(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload);
                _railGraphSnapshotApplier.ApplySnapshot(message.Snapshot);
            }

            void OnTrainUnitFullSnapshot(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(payload);

                // MessagePackのbundleをモデルへ変換してapplierの既存入力型に合わせる
                // Convert bundles to models to reuse the applier's existing input type
                var bundles = new List<TrainUnitSnapshotBundle>(message.Snapshots?.Count ?? 0);
                if (message.Snapshots != null)
                {
                    foreach (var snapshot in message.Snapshots) bundles.Add(snapshot.ToModel());
                }

                var response = new TrainUnitSnapshotResponse(bundles, message.ServerTick, message.UnitsHash, message.WatermarkTickSequenceId);
                _trainSnapshotApplier.ApplySnapshot(response);

                // watermark以下の古いdiff/hashをpurgeし、以後のイベントが連続適用できる状態にする
                // Purge stale diffs/hashes at or below the watermark so later events continue seamlessly
                var watermarkId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.WatermarkTickSequenceId);
                _futureMessageBuffer.DiscardEventsAtOrBelow(watermarkId);
                _futureMessageBuffer.DiscardHashesOlderThan(watermarkId);

                _onFullSnapshotApplied.OnNext(watermarkId);
                _initialApplyCompletion.TrySetResult();
            }

            #endregion
        }

        public void Dispose()
        {
            _railSubscription?.Dispose();
            _trainSubscription?.Dispose();
        }
    }
}
