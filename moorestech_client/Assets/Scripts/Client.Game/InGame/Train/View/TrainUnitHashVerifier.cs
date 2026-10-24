using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.FluidNetwork;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    // Train/Rail/Belt/Fluidのhash gate判定を担当する
    // Handles train/rail/belt/fluid hash gate checks.
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate
    {
        private const uint HashMismatchTickGapThreshold = 200;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly BeltTransportClientCache _beltTransportCache;
        private readonly FluidNetworkClientCache _fluidNetworkCache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private (ulong AppliedId, ulong ExpectedId, TrainTickHashState Server, TrainSynchronizationHashComparison Comparison)? _lastMismatch;

        public TrainUnitHashVerifier(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            BeltTransportClientCache beltTransportCache,
            FluidNetworkClientCache fluidNetworkCache,
            TrainUnitTickState tickState,
            TrainSynchronizationDiagnostics diagnostics)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _beltTransportCache = beltTransportCache;
            _fluidNetworkCache = fluidNetworkCache;
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

                // 同一tickでTrain/Rail/Belt/Fluidのローカルhashを照合する
                // Compare local train/rail/belt/fluid hashes on the same tick
                var appliedId = _tickState.GetAppliedTickUnifiedId();
                var comparison = ResolveComparison(appliedId);
                if (!comparison.HasMismatch)
                {
                    _lastMismatch = null;
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }
                _lastMismatch = (appliedId, currentTickUnifiedId, message, comparison);
                var receivedTickGap = (long)(_tickState.GetMaxBufferedTickUnifiedId() >> 32) - _tickState.GetTick();
                var permanentWait = HashMismatchTickGapThreshold <= receivedTickGap;
                _diagnostics.RecordHashMismatch(currentTickUnifiedId, comparison, permanentWait);
                if (permanentWait) _futureMessageBuffer.StopRetainingFutureMessages("HashMismatchReceivedTickGap");
                return false;
            }

            // 初期同期後の状態変更は順序付き適用だけなので、同じ適用位置・同じサーバーhashなら前回の比較を使い回す
            // After initial sync, only ordered applies change state, so reuse the previous comparison at the same applied position with the same server hashes
            TrainSynchronizationHashComparison ResolveComparison(ulong appliedId)
            {
                if (_lastMismatch is { } previous && previous.AppliedId == appliedId && previous.ExpectedId == currentTickUnifiedId && IsSameServerHash(previous.Server, message))
                {
                    return previous.Comparison;
                }
                return new TrainSynchronizationHashComparison(
                    _trainCache.ComputeCurrentHash(), message.UnitsHash,
                    _railGraphCache.ComputeCurrentHash(), message.RailGraphHash,
                    _beltTransportCache.ComputeCurrentHash(), message.BeltTransportHash,
                    _fluidNetworkCache.CurrentHash, message.FluidNetworkHash);
            }

            static bool IsSameServerHash(in TrainTickHashState a, in TrainTickHashState b)
            {
                return a.UnitsHash == b.UnitsHash && a.RailGraphHash == b.RailGraphHash && a.BeltTransportHash == b.BeltTransportHash && a.FluidNetworkHash == b.FluidNetworkHash;
            }

            #endregion
        }
    }
}
