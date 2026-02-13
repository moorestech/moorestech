using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Train.Network
{
    // 未来tickのTrain/Railイベントを種類別に保持して、シミュレーションtick到達時に適用する。
    // Buffer future train/rail events by phase and apply them when simulation reaches their tick.
    public sealed class TrainUnitFutureMessageBuffer
    {
        private readonly TrainUnitTickState _tickState;
        private readonly SortedDictionary<ulong, ITrainTickBufferedEvent> _futureEvents = new();
        private readonly SortedDictionary<ulong, TrainUnitHashStateMessagePack> _futureHashStates = new();

        public TrainUnitFutureMessageBuffer(TrainUnitTickState tickState)
        {
            _tickState = tickState;
        }

        // イベントを未来tickキューへ積む。
        // Queue a pre-simulation event only when its tick is still in the future.
        public void EnqueueEvent(uint serverTick, uint tickSequenceId, ITrainTickBufferedEvent bufferedEvent)
        {
            if (bufferedEvent == null)
                return;
            var eventTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(serverTick, tickSequenceId);
            if (eventTickUnifiedId <= _tickState.GetAppliedTickUnifiedId())
            {
                // 適用済みの統合順序以下は捨てる。
                // Drop events already covered.
                Debug.LogWarning(
                    "[TrainUnitFutureMessageBuffer] Dropped stale buffered event by tick unified id. " +
                    $"eventTag={bufferedEvent.EventTag}, tickSequenceId={tickSequenceId}, " +
                    $"eventTickUnifiedId={eventTickUnifiedId}, appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }
            _futureEvents[eventTickUnifiedId] = bufferedEvent;
        }

        // ハッシュイベントをtick基準でキューへ積む。
        // Queue hash states by tick for tick-aligned verification.
        public void EnqueueHash(TrainUnitHashStateMessagePack message)
        {
            if (message == null)
            {
                return;
            }
            var messageTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.TickSequenceId);
            _futureHashStates[messageTickUnifiedId] = message;
        }

        // 指定tickのハッシュを取り出す。
        // Dequeue hash state at the specified tick.
        public bool TryDequeueHashAtTickSequenceId(ulong tickSequenceId, out TrainUnitHashStateMessagePack message)
        {
            message = null;

            // 対象tickより古いhashは検証対象外として破棄する。
            // Discard hashes older than the requested tick.
            while (TryGetFirstTickUnifiedId(_futureHashStates, out var firstTickUnifiedId) &&
                firstTickUnifiedId < tickSequenceId)
            {
                _futureHashStates.Remove(firstTickUnifiedId);
            }
            
            if (!_futureHashStates.ContainsKey(tickSequenceId))
                return false;
            message = _futureHashStates[tickSequenceId];
            return true;
            
            #region Internal
            static bool TryGetFirstTickUnifiedId<TValue>(SortedDictionary<ulong, TValue> source, out ulong firstTickUnifiedId)
            {
                using var enumerator = source.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    firstTickUnifiedId = enumerator.Current.Key;
                    return true;
                }
                
                firstTickUnifiedId = 0;
                return false;
            }
            #endregion
        }
        
        public bool TryFlushEvent(uint currentTick, uint tickSequenceId)
        {
            var eventTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(currentTick, tickSequenceId);
            return TryFlushEvent(eventTickUnifiedId);
        }
        public bool TryFlushEvent(ulong eventTickUnifiedId)
        {
            if (!_futureEvents.ContainsKey(eventTickUnifiedId))
                return false;
            var bufferedEvent = _futureEvents[eventTickUnifiedId];
            bufferedEvent.Apply();
            _futureEvents.Remove(eventTickUnifiedId);
            _tickState.RecordAppliedTickUnifiedId(eventTickUnifiedId);
            return true;
        }

    }
}
