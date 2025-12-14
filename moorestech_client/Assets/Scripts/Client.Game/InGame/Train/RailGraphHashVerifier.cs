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
    /// <summary>
    ///     RailGraphのハッシュ整合性をイベント駆動で監視する
    ///     Listens to server hash/tick events and validates client cache consistency
    /// </summary>
    public sealed class RailGraphHashVerifier : IInitializable, IDisposable
    {
        private readonly RailGraphClientCache _cache;
        private readonly RailGraphSnapshotApplier _snapshotApplier;
        private IDisposable _subscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public RailGraphHashVerifier(RailGraphClientCache cache, RailGraphSnapshotApplier snapshotApplier)
        {
            _cache = cache;
            _snapshotApplier = snapshotApplier;
        }

        public void Initialize()
        {
            // ハッシュ状態イベントを購読しクライアント側の検証を開始
            // Subscribe to hash state events and start validating locally
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailGraphHashStateEventPacket.EventTag,
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
            RailGraphHashStateMessagePack message;
            try
            {
                message = MessagePackSerializer.Deserialize<RailGraphHashStateMessagePack>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RailGraphHashVerifier] Failed to parse hash state: {ex}");
                return;
            }

            var clientHash = _cache.ComputeCurrentHash();
            if (clientHash == message.GraphHash)
            {
                _cache.OverrideTick(message.GraphTick);
                return;
            }

            Debug.LogWarning($"[RailGraphHashVerifier] Hash mismatch detected. client={clientHash}, server={message.GraphHash}. Requesting snapshot.");
            if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
            {
                return;
            }

            await RequestSnapshotAsync(message.GraphTick);
        }

        private async UniTask RequestSnapshotAsync(long serverTick)
        {
            var api = ClientContext.VanillaApi?.Response;
            if (api == null)
            {
                Debug.LogWarning("[RailGraphHashVerifier] VanillaApi is not ready; cannot request snapshot.");
                Interlocked.Exchange(ref _resyncInProgress, 0);
                return;
            }

            var cts = new CancellationTokenSource();
            _resyncCancellation = cts;

            try
            {
                var snapshot = await api.GetRailGraphSnapshot(cts.Token);
                if (snapshot == null)
                {
                    Debug.LogWarning("[RailGraphHashVerifier] Snapshot response was null.");
                    return;
                }

                _snapshotApplier.ApplySnapshot(snapshot);
                _cache.OverrideTick(serverTick);
            }
            catch (OperationCanceledException)
            {
                // Disposeでキャンセルされた場合は静かに終了
                // Quietly exit when cancellation occurs
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RailGraphHashVerifier] Snapshot request failed: {ex}");
            }
            finally
            {
                if (_resyncCancellation == cts)
                {
                    _resyncCancellation.Dispose();
                    _resyncCancellation = null;
                }

                Interlocked.Exchange(ref _resyncInProgress, 0);
            }
        }

        private void CancelResync()
        {
            // 実行中の再同期要求があれば停止する
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

        #endregion
    }
}
