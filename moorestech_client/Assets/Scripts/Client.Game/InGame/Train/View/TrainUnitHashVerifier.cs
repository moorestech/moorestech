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
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnostics _diagnostics;

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
            // 古いhashはバッファから捨てる
            // Discard any stale hashes that are older than the current tick
            _futureMessageBuffer.DiscardHashesOlderThan(currentTickUnifiedId);
            
            // 後続の到着にかかわらず、欠けた順序位置を待ち続ける。
            // Keep waiting for the missing ordered position regardless of later arrivals.
            if (!_futureMessageBuffer.TryDequeueHashAtTickSequenceId(currentTickUnifiedId, out var message))
            {
                _diagnostics.RecordMissingOrderedMessage(currentTickUnifiedId);
                return false;
            }
            
            return ValidateCurrentTickHash();

            #region Internal

            bool ValidateCurrentTickHash()
            {
                if (IsDummyHash(message))
                {
                    _tickState.RecordAppliedTickUnifiedId(currentTickUnifiedId);
                    _diagnostics.RecordApplied(currentTickUnifiedId);
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
                    _diagnostics.RecordApplied(currentTickUnifiedId);
                    return true;
                }
                _diagnostics.RecordHashMismatch(currentTickUnifiedId, localTrainHash, message.unitsHash,
                    localRailGraphHash, message.railGraphHash);
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
