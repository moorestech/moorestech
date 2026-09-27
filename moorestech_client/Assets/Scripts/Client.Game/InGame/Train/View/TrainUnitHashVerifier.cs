using System;
using System.Threading;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    // Train/Railのhash gate判定を担当する
    // Handles train/rail hash gate checks.
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
            // 不整合ゲートが閉じている間はtick進行を止める
            // Stop simulation advance while the mismatch gate is closed
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
                    $"tickSequenceId={message.tickSequenceId}. Tick advancement stopped.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                    return false;
                return false;
                
                bool IsDummyHash((uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId) hashState)
                {
                    return hashState.unitsHash == TrainUnitFutureMessageBuffer.DummyHash &&
                           hashState.railGraphHash == TrainUnitFutureMessageBuffer.DummyHash;
                }
            }

            #endregion
        }
    }
}
