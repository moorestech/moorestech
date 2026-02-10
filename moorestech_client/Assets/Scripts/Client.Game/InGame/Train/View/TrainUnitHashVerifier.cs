using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.View
{
    // TrainUnitのハッシュ・tick検証イベントを購読する
    // Listens to server hash/tick events and validates TrainUnit cache consistency
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate, IInitializable, IDisposable
    {
        private readonly TrainUnitSnapshotApplier _snapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _cache;
        private IDisposable _subscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(TrainUnitSnapshotApplier snapshotApplier, TrainUnitFutureMessageBuffer futureMessageBuffer, TrainUnitClientCache cache)
        {
            _snapshotApplier = snapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
            _cache = cache;
        }

        public void Initialize()
        {
            // ハッシュ状態イベントを購読し検証処理を開始する
            // Subscribe to hash state events and start validation
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                TrainUnitHashStateEventPacket.EventTag,
                OnHashStateReceived);
            
            #region Internal            
            void OnHashStateReceived(byte[] payload)
            {
                HandleHashState(payload);
                return;

                void HandleHashState(byte[] payload)
                {
                    var message = MessagePackSerializer.Deserialize<TrainUnitHashStateMessagePack>(payload);
                    if (message == null)
                    {
                        return;
                    }
                    // hashは広義イベントとしてtickバッファへ積む
                    // Buffer hash as a tick-indexed event
                    _futureMessageBuffer.EnqueueHash(message);
                }
            }
            #endregion
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            CancelResync();
            
            #region internal
            void CancelResync()
            {
                // 終了時に進行中の再同期処理をキャンセルする
                // Cancel any in-flight resync operation during shutdown
                var cts = Interlocked.Exchange(ref _resyncCancellation, null);
                if (cts == null) return;
                cts.Cancel();
                cts.Dispose();
                Interlocked.Exchange(ref _resyncInProgress, 0);
            }
            #endregion
        }

        public bool CanAdvanceTick(long currentTick)
        {
            // 現在tickのpre-simハッシュを記録し、受信済みhashを同tickで検証する
            // Record pre-sim hash and validate queued hashes against the same tick
            if (Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 1)
            {
                return false;
            }

            ValidateCurrentTickHash(currentTick);
            return Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 0;
            
            #region internal
            void ValidateCurrentTickHash(long currentTick)
            {
                if (!_futureMessageBuffer.TryDequeueHashAtTick(currentTick, out var message))
                {
                    return;
                }
                var localHash = _cache.ComputeCurrentHash();
                var serverHash = message.UnitsHash;
                if (localHash == serverHash)
                {
                    _futureMessageBuffer.RecordHashVerified(currentTick);
                    return;
                }
                Debug.LogWarning($"[TrainUnitHashVerifier] Hash mismatch detected. tick={currentTick}, client={localHash}, server={serverHash}. Requesting snapshot.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                {
                    return;
                }

                RequestSnapshotAsync().Forget();
                return;
                
                async UniTask RequestSnapshotAsync()
                {
                    var api = ClientContext.VanillaApi.Response;
                    var cts = new CancellationTokenSource();
                    _resyncCancellation = cts;
                    
                    // 再同期スナップショットを取得しキャンセル状態を確認する
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
                    }
                    FinalizeResync(cts);
                    
                    void FinalizeResync(CancellationTokenSource cts)
                    {
                        // 再同期処理の状態を終了処理する
                        // Finalize the resync state.
                        var current = Interlocked.CompareExchange(ref _resyncCancellation, null, cts);
                        if (current == cts) cts.Dispose();
                        Interlocked.Exchange(ref _resyncInProgress, 0);
                    }
                }
            }
            #endregion
        }
    }
}
