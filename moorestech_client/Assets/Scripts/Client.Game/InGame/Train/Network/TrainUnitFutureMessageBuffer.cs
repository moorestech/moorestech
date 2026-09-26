using Client.Game.InGame.Train.Unit;
using Core.Update.TickSynchronization;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Train.Network
{
    // stream内のイベントを統合IDで保持する。
    // Buffer stream events by unified id.
    public sealed class TrainUnitFutureMessageBuffer
    {
        private readonly TrainUnitTickState _tickState;
        private readonly SortedDictionary<ulong, ITrainTickBufferedEvent> _futureEvents = new();

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
            if (!_tickState.TryAcceptReceivedTickUnifiedId(eventTickUnifiedId))
            {
                // 適用済みの統合順序以下は捨てる。
                // Drop events already covered.
                return;
            }
            _futureEvents[eventTickUnifiedId] = bufferedEvent;
        }

        public void DiscardEventsAtOrBelow(ulong tickUnifiedId)
        {
            while (0 < _futureEvents.Count)
            {
                var firstTickUnifiedId = _futureEvents.First().Key;
                if (tickUnifiedId < firstTickUnifiedId) break;
                _futureEvents.Remove(firstTickUnifiedId);
            }
        }

        public bool TryFlushEvent(ulong eventTickUnifiedId)
        {
            if (_tickState.IsStopped || !_futureEvents.ContainsKey(eventTickUnifiedId))
                return false;
            var bufferedEvent = _futureEvents[eventTickUnifiedId];
            bufferedEvent.Apply();

            // 実行済みイベント以下は再適用不要なので一括破棄する。
            // Drop all events at or below executed unified id to prevent re-apply.
            DiscardEventsAtOrBelow(eventTickUnifiedId);
            _tickState.RecordAppliedTickUnifiedId(eventTickUnifiedId);
            return true;
        }
    }
}
