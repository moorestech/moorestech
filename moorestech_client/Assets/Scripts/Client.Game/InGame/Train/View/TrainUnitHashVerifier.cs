using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    // Train/Rail/Beltのhash gate判定を担当する
    // Handles train/rail/belt hash gate checks.
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate
    {
        private const uint HashMismatchTickGapThreshold = 200;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly BeltTransportClientCache _beltTransportCache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private (ulong AppliedId, ulong ExpectedId, uint ServerTrain, uint ServerRail, uint ServerBelt, uint LocalTrain, uint LocalRail, uint LocalBelt)? _lastMismatch;

        public TrainUnitHashVerifier(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            BeltTransportClientCache beltTransportCache,
            TrainUnitTickState tickState,
            TrainSynchronizationDiagnostics diagnostics)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _beltTransportCache = beltTransportCache;
            _tickState = tickState;
            _diagnostics = diagnostics;
        }

        public bool CanAdvanceTick(ulong currentTickUnifiedId)
        {
            if (!_tickState.IsInitialized || _tickState.IsPermanentlyWaiting) return false;
            // 古いhashはバッファから捨てる
            // Discard any stale hashes that are older than the current tick
            _futureMessageBuffer.DiscardHashesOlderThan(currentTickUnifiedId);

            // 後続の到着にかかわらず、欠けた順序位置を待ち続ける。
            // Keep waiting for the missing ordered position regardless of later arrivals.
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
                if (message.IsDummy)
                {
                    _lastMismatch = null;
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }

                // 同一tickでTrain/Rail/Beltのローカルhashを照合する
                // Compare local train/rail/belt hashes on the same tick
                uint localTrainHash;
                uint localRailGraphHash;
                uint localBeltTransportHash;
                var appliedId = _tickState.GetAppliedTickUnifiedId();
                // 初期同期後の状態変更は順序付き適用だけなので、同じ適用位置・入力hashなら前回不一致を再利用する。
                // After initial sync, only ordered applies change state, so reuse mismatches at the same applied position and input hashes.
                if (_lastMismatch is { } previous && previous.AppliedId == appliedId && previous.ExpectedId == currentTickUnifiedId &&
                    previous.ServerTrain == message.UnitsHash && previous.ServerRail == message.RailGraphHash && previous.ServerBelt == message.BeltTransportHash)
                {
                    localTrainHash = previous.LocalTrain;
                    localRailGraphHash = previous.LocalRail;
                    localBeltTransportHash = previous.LocalBelt;
                }
                else
                {
                    localTrainHash = _trainCache.ComputeCurrentHash();
                    localRailGraphHash = _railGraphCache.ComputeCurrentHash();
                    localBeltTransportHash = _beltTransportCache.ComputeCurrentHash();
                }
                var isTrainMismatch = localTrainHash != message.UnitsHash;
                var isRailGraphMismatch = localRailGraphHash != message.RailGraphHash;
                var isBeltTransportMismatch = localBeltTransportHash != message.BeltTransportHash;
                if (!isTrainMismatch && !isRailGraphMismatch && !isBeltTransportMismatch)
                {
                    _lastMismatch = null;
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }
                _lastMismatch = (appliedId, currentTickUnifiedId, message.UnitsHash, message.RailGraphHash, message.BeltTransportHash, localTrainHash, localRailGraphHash, localBeltTransportHash);
                var receivedTickGap = (long)(_tickState.GetMaxBufferedTickUnifiedId() >> 32) - _tickState.GetTick();
                var permanentWait = HashMismatchTickGapThreshold <= receivedTickGap;
                _diagnostics.RecordHashMismatch(currentTickUnifiedId, localTrainHash, message.UnitsHash,
                    localRailGraphHash, message.RailGraphHash, localBeltTransportHash, message.BeltTransportHash, permanentWait);
                if (permanentWait) _futureMessageBuffer.StopRetainingFutureMessages("HashMismatchReceivedTickGap");
                return false;
            }

            #endregion
        }
    }
}
