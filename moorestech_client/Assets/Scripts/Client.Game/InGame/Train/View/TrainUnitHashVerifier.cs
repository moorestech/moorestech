using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    // Train/Railのhash gate判定と不整合時リシンクを担当する
    // Handles train/rail hash gate checks and resync on mismatch.
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate, IDisposable
    {
        private readonly TrainUnitSnapshotApplier _trainSnapshotApplier;
        private readonly RailGraphSnapshotApplier _railGraphSnapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(
            TrainUnitSnapshotApplier trainSnapshotApplier,
            RailGraphSnapshotApplier railGraphSnapshotApplier,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            TrainUnitTickState tickState)
        {
            _trainSnapshotApplier = trainSnapshotApplier;
            _railGraphSnapshotApplier = railGraphSnapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _tickState = tickState;
        }

        public void Dispose()
        {
            CancelResync();

            #region Internal

            void CancelResync()
            {
                // 終了時に進行中の再同期処理をキャンセルする
                // Cancel any in-flight resync operation during shutdown
                var cts = Interlocked.Exchange(ref _resyncCancellation, null);
                if (cts == null)
                    return;
                cts.Cancel();
                cts.Dispose();
                Interlocked.Exchange(ref _resyncInProgress, 0);
            }

            #endregion
        }

        public bool CanAdvanceTick(ulong currentTickUnifiedId)
        {
            // 再同期中はtick進行を止める
            // Stop simulation advance while resync is in progress
            if (Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 1)
                return false;
            // 古いhashはバッファから捨てる
            // Discard any stale hashes that are older than the current tick
            _futureMessageBuffer.DiscardHashesOlderThan(currentTickUnifiedId);
            
            // このtickにメッセージがなく将来tickにメッセージがある場合このtickのメッセージは送られてこない可能性が非常に高い。なのでTickを強制的に進めることにする
            // If there is no message for the current tick but there are messages for future ticks, it's likely that the current tick's message won't arrive. In that case, we will force advance the tick.
            if (!_futureMessageBuffer.TryDequeueHashAtTickSequenceId(currentTickUnifiedId, out var message))
                return _futureMessageBuffer.TryGetFirstHashTickUnifiedId(out _);
            var isVerified = ValidateCurrentTickHash();
            return isVerified && Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 0;

            #region Internal

            bool ValidateCurrentTickHash()
            {
                if (IsDummyHash(message))
                {
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    return true;
                }

                // 同一tickでTrain/Railのローカルhashを照合する
                // Compare local train/rail hashes on the same tick
                var localTrainHash = _trainCache.ComputeCurrentHash();
                var localRailGraphHash = _railGraphCache.ComputeCurrentHash();
                var isTrainMismatch = localTrainHash != message.UnitsHash;
                var isRailGraphMismatch = localRailGraphHash != message.RailGraphHash;
                if (!isTrainMismatch && !isRailGraphMismatch)
                {
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    return true;
                }
                Debug.LogWarning(
                    $"[TrainUnitHashVerifier] Hash mismatch detected. tick={_tickState.GetTick()}, " +
                    $"train(client={localTrainHash}, server={message.UnitsHash}), " +
                    $"rail(client={localRailGraphHash}, server={message.RailGraphHash}), " +
                    $"tickSequenceId={message.TickSequenceId}. Requesting snapshot.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                    return false;
                RequestSnapshotAsync(isRailGraphMismatch).Forget();
                return false;
            }

            async UniTask RequestSnapshotAsync(bool includeRailGraphSnapshot)
            {
                var api = ClientContext.VanillaApi.Response;
                var cts = new CancellationTokenSource();
                _resyncCancellation = cts;

                if (includeRailGraphSnapshot)
                {
                    // rail不整合時はRailGraph+TrainUnitを同時に再同期する
                    // On rail mismatch, resync both rail graph and train snapshots
                    var snapshotResult = await UniTask.WhenAll(
                        api.GetRailGraphSnapshot(cts.Token),
                        api.GetTrainUnitSnapshots(cts.Token)).SuppressCancellationThrow();
                    if (snapshotResult.IsCanceled || cts.IsCancellationRequested)
                    {
                        FinalizeResync(cts);
                        return;
                    }

                    var (railGraphSnapshot, trainSnapshot) = snapshotResult.Result;
                    if (railGraphSnapshot == null || trainSnapshot == null)
                    {
                        Debug.LogWarning("[TrainUnitHashVerifier] Rail+Train snapshot response was null.");
                        FinalizeResync(cts);
                        return;
                    }
                    // 再同期snapshotはキューに積まず即時適用する（rail -> train の順）。
                    // Apply resync snapshots immediately without queueing (rail -> train order).
                    _railGraphSnapshotApplier.ApplySnapshot(railGraphSnapshot);
                    _trainSnapshotApplier.ApplySnapshot(trainSnapshot);
                    FinalizeResync(cts);
                    return;
                }

                // train不整合のみならTrainUnitスナップショットだけ再取得する
                // For train-only mismatch, fetch only train snapshots
                var trainSnapshotResult = await api.GetTrainUnitSnapshots(cts.Token).SuppressCancellationThrow();
                if (trainSnapshotResult.IsCanceled || cts.IsCancellationRequested)
                {
                    FinalizeResync(cts);
                    return;
                }

                var snapshot = trainSnapshotResult.Result;
                if (snapshot == null)
                {
                    Debug.LogWarning("[TrainUnitHashVerifier] Train snapshot response was null.");
                }
                else
                {
                    // trainのみの再同期snapshotも即時適用し、古いキューを破棄する。
                    // Apply train-only resync snapshot immediately and discard stale queued data.
                    _trainSnapshotApplier.ApplySnapshot(snapshot);
                }
                FinalizeResync(cts);

                void FinalizeResync(CancellationTokenSource targetCancellation)
                {
                    // 再同期処理の状態を終了処理する
                    // Finalize resync state and release the gate
                    var current = Interlocked.CompareExchange(ref _resyncCancellation, null, targetCancellation);
                    if (current == targetCancellation)
                    {
                        targetCancellation.Dispose();
                    }
                    Interlocked.Exchange(ref _resyncInProgress, 0);
                }
            }

            bool IsDummyHash(TrainUnitHashStateMessagePack hashState)
            {
                return hashState.UnitsHash == TrainUnitHashStateMessagePack.DummyHash &&
                    hashState.RailGraphHash == TrainUnitHashStateMessagePack.DummyHash;
            }

            #endregion
        }
    }
}
