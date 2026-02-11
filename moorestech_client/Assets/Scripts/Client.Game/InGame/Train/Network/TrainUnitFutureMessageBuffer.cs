using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Server.Util.MessagePack;

namespace Client.Game.InGame.Train.Network
{
    // 将来tick向け列車イベントを保持し、到達時に適用する。
    // Buffer future train events and apply them when simulation reaches their tick.
    public sealed class TrainUnitFutureMessageBuffer
    {
        private readonly TrainUnitTickState _tickState;
        private readonly SortedDictionary<long, List<ITrainTickBufferedEvent>> _futureEvents = new();
        private readonly SortedDictionary<long, uint> _futureHashStates = new();

        public TrainUnitFutureMessageBuffer(TrainUnitTickState tickState)
        {
            _tickState = tickState;
        }

        // 列車イベントを将来tick分だけキューへ積む。
        // Queue a train event only when its tick is still in the future.
        public void Enqueue(long serverTick, ITrainTickBufferedEvent bufferedEvent)
        {
            if (bufferedEvent == null)
            {
                return;
            }
            if (serverTick <= _tickState.GetTick())
            {
                return;
            }

            if (!_futureEvents.TryGetValue(serverTick, out var events))
            {
                events = new List<ITrainTickBufferedEvent>();
                _futureEvents.Add(serverTick, events);
            }
            events.Add(bufferedEvent);
        }

        // ハッシュイベントをtick単位でキューへ積む。
        // Queue hash states by tick for tick-aligned verification.
        public void EnqueueHash(TrainUnitHashStateMessagePack message)
        {
            if (message == null)
            {
                return;
            }
            if (message.TrainTick < _tickState.GetTick())
            {
                return;
            }

            _futureHashStates[message.TrainTick] = message.UnitsHash;
            _tickState.RecordHashReceived(message.TrainTick);
        }

        // 指定tickのハッシュイベントを取り出す。
        // Dequeue hash state at the specified tick.
        public bool TryDequeueHashAtTick(long tick, out TrainUnitHashStateMessagePack message)
        {
            if (_futureHashStates.TryGetValue(tick, out var hash))
            {
                _futureHashStates.Remove(tick);
                message = new TrainUnitHashStateMessagePack(hash, tick);
                return true;
            }
            message = null;
            return false;
        }

        // 到達済みtickまでのキュー済みイベントを適用する。
        // Apply queued events that are now reachable by simulated tick.
        public void FlushBySimulatedTick()
        {
            var simulatedTick = _tickState.GetTick();
            FlushFutureEvents(simulatedTick);

            #region Internal

            void FlushFutureEvents(long currentTick)
            {
                while (TryGetFirstTick(_futureEvents, out var targetTick) && targetTick <= currentTick)
                {
                    var events = _futureEvents[targetTick];
                    for (var i = 0; i < events.Count; i++)
                    {
                        // 受信時に束ねたapply処理をtick到達時に実行する。
                        // Execute the pre-built apply action when the tick is reached.
                        events[i].Apply();
                    }
                    _futureEvents.Remove(targetTick);
                }

                bool TryGetFirstTick<T>(SortedDictionary<long, List<T>> source, out long tick)
                {
                    using var enumerator = source.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        tick = enumerator.Current.Key;
                        return true;
                    }

                    tick = 0;
                    return false;
                }
            }

            #endregion
        }

        // スナップショットで上書き済みtick以下のイベントを破棄する。
        // Discard queued events at or below the tick already covered by snapshot.
        public void DiscardUpToTick(long tick)
        {
            RemoveUpToTickForListDictionary(_futureEvents, tick);
            RemoveUpToTickForValueDictionary(_futureHashStates, tick);

            #region Internal

            void RemoveUpToTickForListDictionary<T>(SortedDictionary<long, List<T>> source, long maxTick)
            {
                while (TryGetFirstTickForListDictionary(source, out var targetTick) && targetTick <= maxTick)
                {
                    source.Remove(targetTick);
                }
            }

            void RemoveUpToTickForValueDictionary<T>(SortedDictionary<long, T> source, long maxTick)
            {
                while (TryGetFirstTickForValueDictionary(source, out var targetTick) && targetTick <= maxTick)
                {
                    source.Remove(targetTick);
                }
            }

            bool TryGetFirstTickForListDictionary<T>(SortedDictionary<long, List<T>> source, out long firstTick)
            {
                using var enumerator = source.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    firstTick = enumerator.Current.Key;
                    return true;
                }

                firstTick = 0;
                return false;
            }

            bool TryGetFirstTickForValueDictionary<T>(SortedDictionary<long, T> source, out long firstTick)
            {
                using var enumerator = source.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    firstTick = enumerator.Current.Key;
                    return true;
                }

                firstTick = 0;
                return false;
            }

            #endregion
        }
    }
}
