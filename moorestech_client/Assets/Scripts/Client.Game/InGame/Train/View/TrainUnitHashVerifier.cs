using System;
using System.Collections.Generic;
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
        private const int HashHistoryCapacity = 256;

        private readonly TrainUnitSnapshotApplier _snapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly Dictionary<long, uint> _localHashHistory = new();
        private readonly Queue<long> _localHashHistoryOrder = new();
        private readonly List<TrainUnitHashStateMessagePack> _hashMessages = new();
        private IDisposable _subscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(TrainUnitSnapshotApplier snapshotApplier, TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _snapshotApplier = snapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
        }

        public void Initialize()
        {
            // ハッシュ状態イベントを購読し検証処理を開始する
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
            ClearLocalHashHistory();
        }

        public bool CanAdvanceTick(long currentTick)
        {
            // 現在tickのpre-simハッシュを記録し、受信済みhashを同tickで検証する
            // Record pre-sim hash and validate queued hashes against the same tick
            if (Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 1)
            {
                return false;
            }

            var localHash = _futureMessageBuffer.ComputeCurrentHash();
            RecordLocalHash(currentTick, localHash);
            ValidatePendingHashes(currentTick);
            return Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 0;
        }

        #region Internal

        private void OnHashStateReceived(byte[] payload)
        {
            HandleHashState(payload);
        }

        private void HandleHashState(byte[] payload)
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

        private void ValidatePendingHashes(long currentTick)
        {
            _futureMessageBuffer.DequeueHashesAtOrBefore(currentTick, _hashMessages);
            for (var i = 0; i < _hashMessages.Count; i++)
            {
                var message = _hashMessages[i];
                var hashTick = message.TrainTick;
                var serverHash = message.UnitsHash;

                if (!_localHashHistory.TryGetValue(hashTick, out var localHash))
                {
                    // 履歴外の古いhashは検証不能なので警告のみでスキップする
                    // Skip too-late hashes that are outside local hash history
                    Debug.LogWarning($"[TrainUnitHashVerifier] Hash arrived too late to validate. hashTick={hashTick}, currentTick={currentTick}.");
                    continue;
                }

                if (localHash == serverHash)
                {
                    _futureMessageBuffer.RecordHashVerified(hashTick);
                    continue;
                }

                Debug.LogWarning($"[TrainUnitHashVerifier] Hash mismatch detected. hashTick={hashTick}, currentTick={currentTick}, client={localHash}, server={serverHash}. Requesting snapshot.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                {
                    return;
                }

                RequestSnapshotAsync(hashTick).Forget();
                return;
            }
        }

        private async UniTask RequestSnapshotAsync(long serverTick)
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
                ClearLocalHashHistory();
            }
            FinalizeResync(cts);
        }

        private void CancelResync()
        {
            // 終了時に進行中の再同期処理をキャンセルする
            // Cancel any in-flight resync operation during shutdown
            var cts = Interlocked.Exchange(ref _resyncCancellation, null);
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }

        private void FinalizeResync(CancellationTokenSource cts)
        {
            // 再同期処理の状態を終了処理する
            // Finalize the resync state.
            var current = Interlocked.CompareExchange(ref _resyncCancellation, null, cts);
            if (current == cts) cts.Dispose();
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }

        private void RecordLocalHash(long tick, uint hash)
        {
            if (_localHashHistory.TryAdd(tick, hash))
            {
                _localHashHistoryOrder.Enqueue(tick);
            }
            else
            {
                _localHashHistory[tick] = hash;
            }

            while (_localHashHistoryOrder.Count > HashHistoryCapacity)
            {
                var oldTick = _localHashHistoryOrder.Dequeue();
                _localHashHistory.Remove(oldTick);
            }
        }

        private void ClearLocalHashHistory()
        {
            _localHashHistory.Clear();
            _localHashHistoryOrder.Clear();
        }

        #endregion
    }
}
