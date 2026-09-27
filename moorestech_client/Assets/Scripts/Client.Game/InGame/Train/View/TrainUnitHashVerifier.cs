using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using UnityEngine;

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

        public TrainUnitHashVerifier(
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            TrainUnitTickState tickState)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _tickState = tickState;
        }

        public bool CanAdvanceTick(ulong currentTickUnifiedId)
        {
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
            
            return ValidateCurrentTickHash();

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
                    $"tickSequenceId={message.tickSequenceId}. Current tick hash validation failed.");
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
