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
        private readonly SortedDictionary<ulong, List<ITrainTickBufferedEvent>> _futurePreEvents = new();
        private readonly SortedDictionary<ulong, List<ITrainTickBufferedEvent>> _futurePostEvents = new();
        private readonly SortedDictionary<ulong, TrainUnitHashStateMessagePack> _futureHashStates = new();
        private readonly SortedSet<uint> _snapshotAppliedTicks = new();

        public TrainUnitFutureMessageBuffer(TrainUnitTickState tickState)
        {
            _tickState = tickState;
        }

        // pre-simイベントを未来tickキューへ積む。
        // Queue a pre-simulation event only when its tick is still in the future.
        public void EnqueuePre(uint serverTick, uint tickSequenceId, ITrainTickBufferedEvent bufferedEvent)
        {
            Enqueue(_futurePreEvents, serverTick, tickSequenceId, bufferedEvent);
        }

        // post-simイベントを未来tickキューへ積む。
        // Queue a post-simulation event only when its tick is still in the future.
        public void EnqueuePost(uint serverTick, uint tickSequenceId, ITrainTickBufferedEvent bufferedEvent)
        {
            Enqueue(_futurePostEvents, serverTick, tickSequenceId, bufferedEvent);
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
            var currentTickFloorUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(_tickState.GetTick(), 0);
            if (messageTickUnifiedId < currentTickFloorUnifiedId)
            {
                // 過去tickのhashは破棄し、原因調査用にログへ残す。
                // Drop stale hash ticks and log for startup gap investigation.
                Debug.LogWarning(
                    "[TrainUnitFutureMessageBuffer] Dropped stale hash message. " +
                    $"serverTick={message.ServerTick}, currentTick={_tickState.GetTick()}, " +
                    $"messageTickUnifiedId={messageTickUnifiedId}, currentTickFloorUnifiedId={currentTickFloorUnifiedId}, " +
                    $"trainHash={message.UnitsHash}, railHash={message.RailGraphHash}, tickSequenceId={message.TickSequenceId}");
                return;
            }
            if (messageTickUnifiedId <= _tickState.GetAppliedTickUnifiedId())
            {
                // 既に適用済みの統合順序以下は破棄する。
                // Drop hashes already covered by applied unified order.
                Debug.LogWarning(
                    "[TrainUnitFutureMessageBuffer] Dropped stale hash by tick unified id. " +
                    $"serverTick={message.ServerTick}, tickSequenceId={message.TickSequenceId}, " +
                    $"messageTickUnifiedId={messageTickUnifiedId}, appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }

            _futureHashStates[messageTickUnifiedId] = message;
            _tickState.RecordHashReceived(message.ServerTick);
        }

        // 指定tickのハッシュを取り出す。
        // Dequeue hash state at the specified tick.
        public bool TryDequeueHashAtTick(uint tick, out TrainUnitHashStateMessagePack message)
        {
            message = null;
            var minTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, 0);
            var maxTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, uint.MaxValue);

            // 対象tickより古いhashは検証対象外として破棄する。
            // Discard hashes older than the requested tick.
            while (TryGetFirstTickUnifiedId(_futureHashStates, out var firstTickUnifiedId) &&
                   firstTickUnifiedId < minTickUnifiedId)
            {
                _futureHashStates.Remove(firstTickUnifiedId);
            }

            // 同一tick内で複数hashがある場合は最大sequenceを採用する。
            // When multiple hashes exist in the same tick, pick the highest sequence one.
            var found = false;
            while (TryGetFirstTickUnifiedId(_futureHashStates, out var targetTickUnifiedId) &&
                   targetTickUnifiedId <= maxTickUnifiedId)
            {
                message = _futureHashStates[targetTickUnifiedId];
                _futureHashStates.Remove(targetTickUnifiedId);
                found = true;
            }

            return found;
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
        public void RecordSnapshotAppliedTick(uint tick)
        {
            _snapshotAppliedTicks.Add(tick);
        }

        // 指定tickのSimulateUpdateをスキップするか判定し、対象なら消費する。
        // Return true when simulation should be skipped for this tick and consume it.
        public bool TryConsumeSimulationSkipTick(uint tick)
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

        // スナップショット基準までのtickUnifiedIdを全キューから破棄する。
        // Discard queued entries whose tickUnifiedId is at or below snapshot baseline.
        public void DiscardUpToTickUnifiedId(ulong tickUnifiedId)
        {
            RemoveUpToUnifiedIdFromListDictionary(_futurePreEvents, tickUnifiedId);
            RemoveUpToUnifiedIdFromListDictionary(_futurePostEvents, tickUnifiedId);
            RemoveUpToUnifiedIdFromValueDictionary(_futureHashStates, tickUnifiedId);
            RemoveUpToTickFromSnapshotAppliedTicks(_snapshotAppliedTicks, TrainTickUnifiedIdUtility.ExtractTick(tickUnifiedId));

            #region Internal

            void RemoveUpToUnifiedIdFromListDictionary<TValue>(SortedDictionary<ulong, List<TValue>> source, ulong maxTickUnifiedId)
            {
                while (TryGetFirstTickUnifiedId(source, out var targetTickUnifiedId) &&
                       targetTickUnifiedId <= maxTickUnifiedId)
                {
                    source.Remove(targetTickUnifiedId);
                }
            }

            void RemoveUpToUnifiedIdFromValueDictionary<TValue>(SortedDictionary<ulong, TValue> source, ulong maxTickUnifiedId)
            {
                while (TryGetFirstTickUnifiedId(source, out var targetTickUnifiedId) &&
                       targetTickUnifiedId <= maxTickUnifiedId)
                {
                    source.Remove(targetTickUnifiedId);
                }
            }

            void RemoveUpToTickFromSnapshotAppliedTicks(SortedSet<uint> source, uint maxTick)
            {
                while (source.Count > 0 && source.Min <= maxTick)
                {
                    source.Remove(source.Min);
                }
            }

            bool TryGetFirstTickUnifiedId<TValue>(SortedDictionary<ulong, TValue> source, out ulong firstTickUnifiedId)
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

        private void Enqueue(SortedDictionary<ulong, List<ITrainTickBufferedEvent>> target, uint serverTick, uint tickSequenceId, ITrainTickBufferedEvent bufferedEvent)
        {
            if (bufferedEvent == null)
            {
                return;
            }
            var eventTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(serverTick, tickSequenceId);
            var currentTickUpperBoundUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(_tickState.GetTick(), uint.MaxValue);
            if (eventTickUnifiedId <= _tickState.GetAppliedTickUnifiedId())
            {
                // スナップショット適用済みの統合順序以下は捨てる。
                // Drop events already covered by applied unified order.
                Debug.LogWarning(
                    "[TrainUnitFutureMessageBuffer] Dropped stale buffered event by tick unified id. " +
                    $"eventTag={bufferedEvent.EventTag}, tickSequenceId={tickSequenceId}, " +
                    $"eventTickUnifiedId={eventTickUnifiedId}, appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }
            if (eventTickUnifiedId <= currentTickUpperBoundUnifiedId)
            {
                // 既に過ぎたtickのイベントは適用できないため破棄する。
                // Drop events targeting past/current ticks because they can no longer be applied deterministically.
                Debug.LogWarning(
                    "[TrainUnitFutureMessageBuffer] Dropped stale buffered event by current tick unified range. " +
                    $"eventTag={bufferedEvent.EventTag}, serverTick={serverTick}, currentTick={_tickState.GetTick()}, " +
                    $"tickSequenceId={tickSequenceId}, eventTickUnifiedId={eventTickUnifiedId}, " +
                    $"currentTickUpperBoundUnifiedId={currentTickUpperBoundUnifiedId}");
                return;
            }

            if (!target.TryGetValue(eventTickUnifiedId, out var events))
            {
                events = new List<ITrainTickBufferedEvent>();
                target.Add(eventTickUnifiedId, events);
            }
            events.Add(bufferedEvent);
        }

        private void FlushFutureEvents(SortedDictionary<ulong, List<ITrainTickBufferedEvent>> source, uint currentTick)
        {
            var currentTickUpperBoundUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(currentTick, uint.MaxValue);
            while (TryGetFirstTickUnifiedId(source, out var targetTickUnifiedId) &&
                   targetTickUnifiedId <= currentTickUpperBoundUnifiedId)
            {
                var events = source[targetTickUnifiedId];
                for (var i = 0; i < events.Count; i++)
                {
                    events[i].Apply();
                }
                source.Remove(targetTickUnifiedId);
                _tickState.RecordAppliedTickUnifiedId(
                    TrainTickUnifiedIdUtility.ExtractTick(targetTickUnifiedId),
                    TrainTickUnifiedIdUtility.ExtractTickSequenceId(targetTickUnifiedId));
            }

            bool TryGetFirstTickUnifiedId(SortedDictionary<ulong, List<ITrainTickBufferedEvent>> sourceDictionary, out ulong tickUnifiedId)
            {
                using var enumerator = sourceDictionary.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    tickUnifiedId = enumerator.Current.Key;
                    return true;
                }

                tickUnifiedId = 0;
                return false;
            }
        }

        private static bool TryGetFirstTickUnifiedId<TValue>(SortedDictionary<ulong, TValue> source, out ulong firstTickUnifiedId)
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
    }
}
