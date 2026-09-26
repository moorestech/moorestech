using Client.Game.Common;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network.TickSynchronization;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    // 実hash不一致時はstream停止と保存なし終了を行う
    // Stop the stream and exit without saving on a proven hash mismatch
    public sealed class TrainUnitHashVerifier : ITrainUnitHashTickGate
    {
        private readonly TrainUnitHashBuffer _futureMessageBuffer;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;

        public TrainUnitHashVerifier(TrainTickContext context, TrainUnitClientCache trainCache, RailGraphClientCache railGraphCache)
        {
            _futureMessageBuffer = context.Hashes;
            _tickState = context.State;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
        }

        public bool CanAdvanceTick(ulong currentTickUnifiedId)
        {
            if (_tickState.IsStopped) return false;
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
            
            var isVerified = ValidateCurrentTickHash();
            return isVerified;

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
                _tickState.Stop(
                    $"[TrainUnitHashVerifier] Hash mismatch detected. tick={_tickState.GetTick()}, " +
                    $"train(client={localTrainHash}, server={message.unitsHash}), " +
                    $"rail(client={localRailGraphHash}, server={message.railGraphHash}), " +
                    $"tickSequenceId={message.tickSequenceId}. Exiting without saving.");
                GameShutdownEvent.QuitAfterSynchronizationFailure();
                return false;
                
                bool IsDummyHash((uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId) hashState)
                {
                    return hashState.unitsHash == TrainUnitHashBuffer.DummyHash &&
                           hashState.railGraphHash == TrainUnitHashBuffer.DummyHash;
                }
            }

            #endregion
        }
    }
}
