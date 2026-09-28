using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.View;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
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
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private readonly TrainUnitTickState _tickState;
        private IDisposable _railSubscription;
        private IDisposable _trainSubscription;

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
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainSynchronizationDiagnostics diagnostics,
            TrainUnitTickState tickState)
        {
            _railGraphSnapshotApplier = railGraphSnapshotApplier;
            _trainSnapshotApplier = trainSnapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
            _diagnostics = diagnostics;
            _tickState = tickState;
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
                if (message.Snapshot != null)
                {
                    _tickState.RecordReceivedTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.Snapshot.GraphTick, message.Snapshot.GraphTickSequenceId));
                    _diagnostics.RecordReceived("RailGraphFullSnapshot", message.Snapshot.GraphTick, message.Snapshot.GraphTickSequenceId);
                }
                if (!CanApplyInitialSnapshot()) return;
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
                _diagnostics.RecordReceived("TrainUnitFullSnapshot", message.ServerTick, message.WatermarkTickSequenceId);
                _tickState.RecordReceivedTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.WatermarkTickSequenceId));
                if (!CanApplyInitialSnapshot()) return;

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

                // 同期継続が走る完了通知より先に、runtimeの初期状態を確定する。
                // Establish runtime initialization before completion can execute synchronous continuations.
                if (_initialApplyCompletion.Task.Status == UniTaskStatus.Pending)
                {
                    _tickState.Initialize(watermarkId);
                    _initialApplyCompletion.TrySetResult();
                }
            }
            catch (Exception applyException)
            {
                // 畳んでここで止める理由はレール側と同じ
                // Folded and stopped here for the same reason as the rail side
                _initialApplyCompletion.TrySetException(applyException);
                Debug.LogError($"[TrainFullSnapshot] trainUnitの適用に失敗しました: {applyException}");
            }
        }

        private bool CanApplyInitialSnapshot()
        {
            if (_initialApplyCompletion.Task.Status == UniTaskStatus.Pending) return true;
            // 失敗・恒久停止の理由は通知済み。完了済みsnapshotの再適用だけを説明する。
            // Failure and permanent-stop reasons were already logged; explain only completed-snapshot rejection.
            if (_initialApplyCompletion.Task.Status == UniTaskStatus.Succeeded && !_tickState.IsPermanentlyWaiting)
                Debug.LogWarning("[TrainFullSnapshot] Ignored full snapshot after initial synchronization completed.");
            return false;
        }

        public void Dispose()
        {
            _railSubscription?.Dispose();
            _trainSubscription?.Dispose();
        }
    }
}
