using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    // Train/Railのhash gate判定と不整合時リシンクを担当する
    // Handles train/rail hash gate checks and resync on mismatch.
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate, IDisposable
    {
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private readonly IDisposable _fullSnapshotSubscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(
            TrainFullSnapshotEventNetworkHandler fullSnapshotEventNetworkHandler,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            TrainUnitTickState tickState)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _tickState = tickState;

            // full snapshot適用完了でresyncゲートを解除する（適用自体はhandlerが担う）
            // Release the resync gate on full-snapshot application; the handler owns the apply itself
            _fullSnapshotSubscription = fullSnapshotEventNetworkHandler.OnFullSnapshotApplied.Subscribe(_ => ReleaseResyncGate());
        }

        private void ReleaseResyncGate()
        {
            var cts = Interlocked.Exchange(ref _resyncCancellation, null);
            cts?.Dispose();
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }

        public void Dispose()
        {
            _fullSnapshotSubscription?.Dispose();
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
            {
                // バッファが空なら次バンドル待ちの正常状態なので警告しない
                // An empty buffer just means waiting for the next bundle, so stay silent
                if (!_futureMessageBuffer.TryGetFirstHashTickUnifiedId(out var firstBufferedTickUnifiedId))
                    return false;
                Debug.LogWarning(
                    $"tick force slip! expected={currentTickUnifiedId >> 32}_{(uint)currentTickUnifiedId}, " +
                    $"firstBuffered={firstBufferedTickUnifiedId >> 32}_{(uint)firstBufferedTickUnifiedId}");
                return true;
            }
            
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
                var isTrainMismatch = localTrainHash != message.unitsHash;
                var isRailGraphMismatch = localRailGraphHash != message.railGraphHash;
                if (!isTrainMismatch && !isRailGraphMismatch)
                {
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    return true;
                }
                Debug.LogWarning(
                    $"[TrainUnitHashVerifier] Hash mismatch detected. tick={_tickState.GetTick()}, " +
                    $"train(client={localTrainHash}, server={message.unitsHash}), " +
                    $"rail(client={localRailGraphHash}, server={message.railGraphHash}), " +
                    $"tickSequenceId={message.tickSequenceId}. Requesting snapshot.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                    return false;
                RequestResyncAsync(isRailGraphMismatch).Forget();
                return false;
                
                bool IsDummyHash((uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId) hashState)
                {
                    return hashState.unitsHash == TrainUnitFutureMessageBuffer.DummyHash &&
                           hashState.railGraphHash == TrainUnitFutureMessageBuffer.DummyHash;
                }
            }

            async UniTask RequestResyncAsync(bool includeRailGraph)
            {
                var api = ClientContext.VanillaApi.Response;
                var cts = new CancellationTokenSource();
                _resyncCancellation = cts;

                // 引き金だけ送る。snapshotはイベント経路で届き、適用完了通知でゲートが解除される
                // Send only the trigger; the snapshot arrives via the event stream and releases the gate on apply
                var ackResult = await api.SendTrainResync(includeRailGraph, cts.Token).SuppressCancellationThrow();
                if (ackResult.IsCanceled || ackResult.Result == null)
                {
                    // ack失敗（タイムアウト等）はゲートを解放して次回のhash検証で再試行する
                    // On ack failure (e.g. timeout) release the gate and let the next hash check retry
                    Debug.LogWarning("[TrainUnitHashVerifier] Resync trigger failed. Releasing gate for retry.");
                    ReleaseResyncGate();
                }
            }
            #endregion
        }
    }
}
