using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    // Train/Railのhash gate判定を担当する
    // Handles train/rail hash gate checks.
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate
    {
        private const uint HashMismatchTickGapThreshold = 200;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private (ulong AppliedId, ulong ExpectedId, uint ServerTrain, uint ServerRail, uint LocalTrain, uint LocalRail)? _lastMismatch;

        public TrainUnitHashVerifier(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            TrainUnitTickState tickState,
            TrainSynchronizationDiagnostics diagnostics)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _tickState = tickState;
            _diagnostics = diagnostics;
        }

        public bool CanAdvanceTick(ulong currentTickUnifiedId)
        {
            if (!_tickState.IsInitialized || _tickState.IsPermanentlyWaiting) return false;
            // 古いhashはバッファから捨てる
            // Discard any stale hashes that are older than the current tick
            _futureMessageBuffer.DiscardHashesOlderThan(currentTickUnifiedId);
            
            // 後続が到着済みなら順序付き通知の欠落を確定する。
            // Confirm a missing ordered message when a later message has already arrived.
            if (!_futureMessageBuffer.TryDequeueHashAtTickSequenceId(currentTickUnifiedId, out var message))
            {
                var confirmedGap = currentTickUnifiedId < _tickState.GetMaxBufferedTickUnifiedId();
                _diagnostics.RecordMissingOrderedMessage(currentTickUnifiedId, confirmedGap);
                if (confirmedGap) _futureMessageBuffer.StopRetainingFutureMessages("ConfirmedOrderedGap");
                return false;
            }
            
            return ValidateCurrentTickHash();

            #region Internal

            bool ValidateCurrentTickHash()
            {
                if (IsDummyHash(message))
                {
                    _lastMismatch = null;
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }

                // 同一tickでTrain/Railのローカルhashを照合する
                // Compare local train/rail hashes on the same tick
                uint localTrainHash;
                uint localRailGraphHash;
                var appliedId = _tickState.GetAppliedTickUnifiedId();
                // 初期同期後の状態変更は順序付き適用だけなので、同じ適用位置・入力hashなら前回不一致を再利用する。
                // After initial sync, only ordered applies change state, so reuse mismatches at the same applied position and input hashes.
                if (_lastMismatch is { } previous && previous.AppliedId == appliedId && previous.ExpectedId == currentTickUnifiedId &&
                    previous.ServerTrain == message.unitsHash && previous.ServerRail == message.railGraphHash)
                {
                    localTrainHash = previous.LocalTrain;
                    localRailGraphHash = previous.LocalRail;
                }
                else
                {
                    localTrainHash = _trainCache.ComputeCurrentHash();
                    localRailGraphHash = _railGraphCache.ComputeCurrentHash();
                }
                var isTrainMismatch = localTrainHash != message.unitsHash;
                var isRailGraphMismatch = localRailGraphHash != message.railGraphHash;
                if (!isTrainMismatch && !isRailGraphMismatch)
                {
                    _lastMismatch = null;
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }
                _lastMismatch = (appliedId, currentTickUnifiedId, message.unitsHash, message.railGraphHash, localTrainHash, localRailGraphHash);
                var receivedTickGap = (long)(_tickState.GetMaxBufferedTickUnifiedId() >> 32) - _tickState.GetTick();
                var permanentWait = HashMismatchTickGapThreshold <= receivedTickGap;
                _diagnostics.RecordHashMismatch(currentTickUnifiedId, localTrainHash, message.unitsHash,
                    localRailGraphHash, message.railGraphHash, permanentWait);
                if (permanentWait) _futureMessageBuffer.StopRetainingFutureMessages("HashMismatchReceivedTickGap");
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
