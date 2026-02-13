using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.View
{
    // Train/Railのハッシュ・tick検証イベントを購読する
    // Listen to hash/tick events and validate train/rail cache consistency
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate, IInitializable, IDisposable
    {
        private const string TrainSnapshotPostEventTag = "va:event:trainUnitSnapshotPost";
        private const string RailSnapshotPostEventTag = "va:event:railGraphSnapshotPost";
        private readonly TrainUnitSnapshotApplier _trainSnapshotApplier;
        private readonly RailGraphSnapshotApplier _railGraphSnapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private IDisposable _subscription;
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

        public void Initialize()
        {
            // 統合ハッシュ状態イベントを購読して検証処理を開始する
            // Subscribe unified hash state events and start validation
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                TrainUnitHashStateEventPacket.EventTag,
                OnHashStateReceived);

            #region Internal

            void OnHashStateReceived(byte[] payload)
            {
                HandleHashState(payload);
                return;

                void HandleHashState(byte[] hashPayload)
                {
                    var message = MessagePackSerializer.Deserialize<TrainUnitHashStateMessagePack>(hashPayload);
                    if (message == null)
                    {
                        return;
                    }
                    // hashはtickバッファへ積み、シミュレーションtickで照合する
                    // Enqueue hash and verify on simulation tick boundary
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

            #region Internal

            void CancelResync()
            {
                // 終了時に進行中の再同期処理をキャンセルする
                // Cancel any in-flight resync operation during shutdown
                var cts = Interlocked.Exchange(ref _resyncCancellation, null);
                if (cts == null)
                {
                    return;
                }
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
            {
                return false;
            }

            if (currentTickUnifiedId <= _tickState.GetAppliedTickUnifiedId())
            {
                return true;
            }

            var isVerified = ValidateCurrentTickHash(currentTickUnifiedId);
            return isVerified && Interlocked.CompareExchange(ref _resyncInProgress, 0, 0) == 0;

            #region Internal

            bool ValidateCurrentTickHash(ulong tickUnifiedId)
            {
                if (!_futureMessageBuffer.TryDequeueHashAtTickSequenceId(tickUnifiedId, out var message))
                {
                    return false;
                }
                // 同一tickでTrain/Railのローカルhashを照合する
                // Compare local train/rail hashes on the same tick
                var localTrainHash = _trainCache.ComputeCurrentHash();
                var localRailGraphHash = _railGraphCache.ComputeCurrentHash();
                var isTrainMismatch = localTrainHash != message.UnitsHash;
                var isRailGraphMismatch = localRailGraphHash != message.RailGraphHash;
                if (!isTrainMismatch && !isRailGraphMismatch)
                {
                    _tickState.RecordAppliedTickUnifiedId(tickUnifiedId);
                    return true;
                }

                Debug.LogWarning(
                    $"[TrainUnitHashVerifier] Hash mismatch detected. tick={_tickState.GetTick()}, " +
                    $"train(client={localTrainHash}, server={message.UnitsHash}), " +
                    $"rail(client={localRailGraphHash}, server={message.RailGraphHash}), " +
                    $"tickSequenceId={message.TickSequenceId}. Requesting snapshot.");
                if (Interlocked.CompareExchange(ref _resyncInProgress, 1, 0) == 1)
                {
                    return false;
                }

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

                    EnqueueRailSnapshotAsPost(railGraphSnapshot);
                    EnqueueTrainSnapshotAsPost(trainSnapshot);
                    _tickState.RecordAppliedTickUnifiedId(railGraphSnapshot.GraphTick, railGraphSnapshot.GraphTickSequenceId);
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
                    EnqueueTrainSnapshotAsPost(snapshot);
                    _tickState.RecordAppliedTickUnifiedId(snapshot.ServerTick, snapshot.TickSequenceId);
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

                void EnqueueRailSnapshotAsPost(RailGraphSnapshotMessagePack railGraphSnapshot)
                {
                    // snapshot応答をpostイベントとして同じtick順序キューへ流す。
                    // Enqueue snapshot response into the same post-event order queue.
                    _futureMessageBuffer.EnqueueEvent(
                        railGraphSnapshot.GraphTick,
                        railGraphSnapshot.GraphTickSequenceId,
                        TrainTickBufferedEvent.Create(RailSnapshotPostEventTag, ApplyRailSnapshot));

                    void ApplyRailSnapshot()
                    {
                        _railGraphSnapshotApplier.ApplySnapshot(railGraphSnapshot);
                    }
                }

                void EnqueueTrainSnapshotAsPost(Client.Network.API.TrainUnitSnapshotResponse trainSnapshot)
                {
                    // snapshot応答をpostイベントとして同じtick順序キューへ流す。
                    // Enqueue snapshot response into the same post-event order queue.
                    _futureMessageBuffer.EnqueueEvent(
                        trainSnapshot.ServerTick,
                        trainSnapshot.TickSequenceId,
                        TrainTickBufferedEvent.Create(TrainSnapshotPostEventTag, ApplyTrainSnapshot));

                    void ApplyTrainSnapshot()
                    {
                        _trainSnapshotApplier.ApplySnapshot(trainSnapshot);
                    }
                }
            }

            #endregion
        }
    }
}
