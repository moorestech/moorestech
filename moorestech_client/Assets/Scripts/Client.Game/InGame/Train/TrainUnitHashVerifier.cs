using System;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    // TrainUnitのハッシュ整合性をイベント駆動で監視する
    // Listens to server hash/tick events and validates TrainUnit cache consistency
    public sealed class TrainUnitHashVerifier : IInitializable, IDisposable
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitSnapshotApplier _snapshotApplier;
        private IDisposable _subscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(TrainUnitClientCache cache, TrainUnitSnapshotApplier snapshotApplier)
        {
            _cache = cache;
            _snapshotApplier = snapshotApplier;
        }

        public void Initialize()
        {
            // ハッシュ状態イベントを購読して整合性検証を開始する
            // Subscribe to hash state events and start validation
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                TrainUnitHashStateEventPacket.EventTag,
                OnHashStateReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            CancelResync();
        }

        #region Internal

        private void OnHashStateReceived(byte[] payload)
        {
            HandleHashStateAsync(payload).Forget();
        }

        private async UniTaskVoid HandleHashStateAsync(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<TrainUnitHashStateMessagePack>(payload);
            var lastTick = _cache.LastServerTick;
            if (message.TrainTick < lastTick)
            {
                // 過去Tickの通知は検証対象外にする
                // Ignore hash states that are older than the local tick
                return;
            }

            var clientHash = _cache.ComputeCurrentHash();
            if (clientHash == message.UnitsHash)
            {
                _cache.OverrideTick(message.TrainTick);
                return;
            }

            Debug.LogWarning($"[TrainUnitHashVerifier] Hash mismatch detected. client={clientHash}, server={message.UnitsHash}. Requesting snapshot.");
            if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
            {
                return;
            }

            await RequestSnapshotAsync(message.TrainTick);
        }

        private async UniTask RequestSnapshotAsync(long serverTick)
        {
            var api = ClientContext.VanillaApi.Response;
            var cts = new CancellationTokenSource();
            _resyncCancellation = cts;

            // 再同期スナップショットを取得しキャンセル状態を判定する
            // Fetch the resync snapshot and check cancellation state.
            var snapshotResult = await api.GetTrainUnitSnapshots(cts.Token).SuppressCancellationThrow();
            if (snapshotResult.IsCanceled || cts.IsCancellationRequested)
            {
                FinalizeResync(cts);
                return;
            }

            var snapshot = snapshotResult.Result;
            if (snapshot == null)
            {
                Debug.LogWarning("[TrainUnitHashVerifier] Snapshot response was null.");
            }
            else
            {
                _snapshotApplier.ApplySnapshot(snapshot);
                _cache.OverrideTick(serverTick);
            }
            FinalizeResync(cts);
        }

        private void CancelResync()
        {
            // 実行中の再同期があれば停止する
            // Cancel any in-flight resync operation during shutdown
            if (_resyncCancellation == null)
            {
                return;
            }
            _resyncCancellation.Cancel();
            _resyncCancellation.Dispose();
            _resyncCancellation = null;
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }

        private void FinalizeResync(CancellationTokenSource cts)
        {
            // 再同期の後始末を行う
            // Finalize the resync state.
            if (_resyncCancellation == cts)
            {
                _resyncCancellation.Dispose();
                _resyncCancellation = null;
            }
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }

        #endregion
    }
}
