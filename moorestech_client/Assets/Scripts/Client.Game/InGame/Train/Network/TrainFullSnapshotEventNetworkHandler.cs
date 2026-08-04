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
using Debug = UnityEngine.Debug;

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

        // 適用完了の通知口。タスクを所有しないため完了ソースで表し、trainUnit適用で満了・rail/train片方の失敗で失格になる
        // Completion source for the apply: owning no task, it is fulfilled by the trainUnit apply and failed by either side
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
            _railSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, HandleRailGraphFullSnapshot);
            _trainSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, HandleTrainUnitFullSnapshot);
        }

        // ネットワーク受信payloadのデシリアライズと適用を隔離する外部境界。畳まないと完了ソースがPendingで残りWhenAllが無期限待機に化ける
        // External boundary isolating deserialization and apply of a received network payload; without folding, the source stays Pending and WhenAll hangs
        private void HandleRailGraphFullSnapshot(byte[] payload)
        {
            try
            {
                var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload);
                _railGraphSnapshotApplier.ApplySnapshot(message.Snapshot);
            }
            catch (Exception applyException)
            {
                // 完了ソースへ畳んで待機境界へ届け、ここで止める。初期snapshotはInitializeDispatchの同期replayを通るため、再送出すると起動ごと中断し残りのbufferedイベントが永久に配信されない
                // Fold into the completion source and stop here: the initial snapshot arrives through InitializeDispatch's synchronous replay, so rethrowing would abort startup and strand every remaining buffered event
                _initialApplyCompletion.TrySetException(applyException);
                Debug.LogError($"[TrainFullSnapshot] railGraphの適用に失敗しました: {applyException}");
            }
        }

        // レール側と同じくネットワークpayloadを隔離する外部境界
        // The same external boundary as the rail side, isolating a received network payload
        private void HandleTrainUnitFullSnapshot(byte[] payload)
        {
            try
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

                // 適用完了を先に確定させる。OnNextは購読者を同期実行するため、購読者の例外で起動が失敗扱いになるのを防ぐ
                // Settle the apply first: OnNext runs subscribers synchronously, so a subscriber throwing must not mark startup as failed
                _initialApplyCompletion.TrySetResult();
                _onFullSnapshotApplied.OnNext(watermarkId);
            }
            catch (Exception applyException)
            {
                // 畳んでここで止める理由はレール側と同じ
                // Folded and stopped here for the same reason as the rail side
                _initialApplyCompletion.TrySetException(applyException);
                Debug.LogError($"[TrainFullSnapshot] trainUnitの適用に失敗しました: {applyException}");
            }
        }

        public void Dispose()
        {
            _railSubscription?.Dispose();
            _trainSubscription?.Dispose();
        }
    }
}
