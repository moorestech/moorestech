using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Server.Util.MessagePack;

namespace Client.Game.InGame.Train.Network
{
    // 未来tickのTrainUnitイベントを種類別に保持して、シミュレーションtick到達時に適用する。
    // Buffer future train events by phase and apply them when simulation reaches their tick.
    public sealed class TrainUnitFutureMessageBuffer
    {
        private readonly TrainUnitTickState _tickState;
        private readonly SortedDictionary<long, List<ITrainTickBufferedEvent>> _futurePreEvents = new();
        private readonly SortedDictionary<long, List<ITrainTickBufferedEvent>> _futurePostEvents = new();
        private readonly SortedDictionary<long, uint> _futureHashStates = new();
        private readonly SortedSet<long> _snapshotAppliedTicks = new();

        public TrainUnitFutureMessageBuffer(TrainUnitTickState tickState)
        {
            _tickState = tickState;
        }

        // pre-simイベントを未来tickキューへ積む。
        // Queue a pre-simulation event only when its tick is still in the future.
        public void EnqueuePre(long serverTick, ITrainTickBufferedEvent bufferedEvent)
        {
            Enqueue(_futurePreEvents, serverTick, bufferedEvent);
        }

        // post-simイベントを未来tickキューへ積む。
        // Queue a post-simulation event only when its tick is still in the future.
        public void EnqueuePost(long serverTick, ITrainTickBufferedEvent bufferedEvent)
        {
            Enqueue(_futurePostEvents, serverTick, bufferedEvent);
        }

        // ハッシュイベントをtick基準でキューへ積む。
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

        // 指定tickのハッシュを取り出す。
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

        // 現在tickで適用可能なpre-simイベントを適用する。
        // Apply pre-simulation events that are now reachable by simulated tick.
        public void FlushPreBySimulatedTick()
        {
            FlushFutureEvents(_futurePreEvents, _tickState.GetTick());
        }

        // 現在tickで適用可能なpost-simイベントを適用する。
        // Apply post-simulation events that are now reachable by simulated tick.
        public void FlushPostBySimulatedTick()
        {
            FlushFutureEvents(_futurePostEvents, _tickState.GetTick());
        }

        // スナップショットを即時適用したtickを記録する。
        // Record the tick where snapshot has already been applied immediately.
        public void RecordSnapshotAppliedTick(long tick)
        {
            _snapshotAppliedTicks.Add(tick);
        }

        // 指定tickのSimulateUpdateをスキップするか判定し、対象なら消費する。
        // Return true when simulation should be skipped for this tick and consume it.
        public bool TryConsumeSimulationSkipTick(long tick)
        {
            while (_snapshotAppliedTicks.Count > 0 && _snapshotAppliedTicks.Min < tick)
            {
                _snapshotAppliedTicks.Remove(_snapshotAppliedTicks.Min);
            }

            if (!_snapshotAppliedTicks.Contains(tick))
            {
                return false;
            }

            _snapshotAppliedTicks.Remove(tick);
            return true;
        }

        // スナップショット適用済みtick以下のキューを破棄する。
        // Discard queued events at or below the tick already covered by snapshot.
        public void DiscardUpToTick(long tick)
        {
            RemoveUpToTickForListDictionary(_futurePreEvents, tick);
            RemoveUpToTickForListDictionary(_futurePostEvents, tick);
            RemoveUpToTickForValueDictionary(_futureHashStates, tick);
            RemoveUpToTickForSortedSet(_snapshotAppliedTicks, tick);

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

            void RemoveUpToTickForSortedSet(SortedSet<long> source, long maxTick)
            {
                while (source.Count > 0 && source.Min <= maxTick)
                {
                    source.Remove(source.Min);
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

        private void Enqueue(SortedDictionary<long, List<ITrainTickBufferedEvent>> target, long serverTick, ITrainTickBufferedEvent bufferedEvent)
        {
            if (bufferedEvent == null)
            {
                return;
            }
            if (serverTick <= _tickState.GetTick())
            {
                return;
            }

            if (!target.TryGetValue(serverTick, out var events))
            {
                events = new List<ITrainTickBufferedEvent>();
                target.Add(serverTick, events);
            }
            events.Add(bufferedEvent);
        }

        private static void FlushFutureEvents(SortedDictionary<long, List<ITrainTickBufferedEvent>> source, long currentTick)
        {
            while (TryGetFirstTick(source, out var targetTick) && targetTick <= currentTick)
            {
                var events = source[targetTick];
                for (var i = 0; i < events.Count; i++)
                {
                    events[i].Apply();
                }
                source.Remove(targetTick);
            }

            bool TryGetFirstTick<T>(SortedDictionary<long, List<T>> sourceDictionary, out long tick)
            {
                using var enumerator = sourceDictionary.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    tick = enumerator.Current.Key;
                    return true;
                }

                tick = 0;
                return false;
            }
        }
    }
}
