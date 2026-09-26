using Core.Update.TickSynchronization;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.Common.TickSynchronization
{
    // stream内のイベントを統合IDで保持する。
    // Buffer stream events by unified id.
    internal sealed class TickEventBuffer
    {
        private readonly ClientTickState _tickState;
        private readonly SortedDictionary<ulong, ITickBufferedEvent> _futureEvents = new();

        public TickEventBuffer(ClientTickState tickState)
        {
            _tickState = tickState;
        }

        // イベントを未来tickキューへ積む。
        // Queue a pre-simulation event only when its tick is still in the future.
        public void EnqueueEvent(uint serverTick, uint tickSequenceId, ITickBufferedEvent bufferedEvent)
        {
            if (bufferedEvent == null)
                return;
            var eventTickUnifiedId = TickUnifiedIdUtility.CreateTickUnifiedId(serverTick, tickSequenceId);
            if (eventTickUnifiedId <= _tickState.GetAppliedTickUnifiedId())
            {
                // 適用済みの統合順序以下は捨てる。
                // Drop events already covered.
                return;
            }
            _tickState.SetMaxBufferedTicks(serverTick);
            _futureEvents[eventTickUnifiedId] = bufferedEvent;
        }

        // full snapshot適用時に呼ぶ
        // Called when a full snapshot is applied
        public void DiscardEventsAtOrBelow(ulong tickUnifiedId)
        {
            while (0 < _futureEvents.Count)
            {
                var firstTickUnifiedId = _futureEvents.First().Key;
                if (tickUnifiedId < firstTickUnifiedId) break;
                _futureEvents.Remove(firstTickUnifiedId);
            }
        }

        public bool TryFlushEvent(uint currentTick, uint tickSequenceId)
        {
            var eventTickUnifiedId = TickUnifiedIdUtility.CreateTickUnifiedId(currentTick, tickSequenceId);
            return TryFlushEvent(eventTickUnifiedId);
        }
        public bool TryFlushEvent(ulong eventTickUnifiedId)
        {
            if (!_futureEvents.ContainsKey(eventTickUnifiedId))
                return false;
            var bufferedEvent = _futureEvents[eventTickUnifiedId];
            bufferedEvent.Apply();

            // 実行済みイベント以下は再適用不要なので一括破棄する。
            // Drop all events at or below executed unified id to prevent re-apply.
            RemoveEventsAtOrBelow(eventTickUnifiedId);
            _tickState.RecordAppliedTickUnifiedId(eventTickUnifiedId);
            return true;

            #region Internal
            void RemoveEventsAtOrBelow(ulong maxTickUnifiedId)
            {
                while (TryGetFirstTickUnifiedId(_futureEvents, out var firstTickUnifiedId) &&
                    firstTickUnifiedId <= maxTickUnifiedId)
                {
                    _futureEvents.Remove(firstTickUnifiedId);
                }
            }

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
    }
}
