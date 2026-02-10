using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using Server.Util.MessagePack;

namespace Client.Game.InGame.Train.Network
{
    // 将来tick向け列車イベントを保持し、到達時に適用する。
    // Buffer future train events and apply them when simulation reaches their tick.
    public sealed class TrainUnitFutureMessageBuffer
    {
        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly SortedDictionary<long, List<TrainUnitSnapshotBundle>> _futureCreatedSnapshots = new();
        private readonly SortedDictionary<long, List<TrainDiagramEventMessagePack>> _futureDiagramEvents = new();
        private readonly SortedDictionary<long, uint> _futureHashStates = new();

        public TrainUnitFutureMessageBuffer(TrainUnitClientCache cache, TrainUnitTickState tickState, TrainCarObjectDatastore trainCarDatastore)
        {
            _cache = cache;
            _tickState = tickState;
            _trainCarDatastore = trainCarDatastore;
        }

        // hash検証通過tickを記録する。
        // Record a tick that passed hash verification.
        public void RecordHashVerified(long tick)
        {
            _tickState.RecordHashVerified(tick);
        }

        // 生成イベントを将来tick分だけキューへ積む。
        // Queue creation events only when their tick is still in the future.
        public void EnqueueCreated(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            if (serverTick <= _tickState.GetSimulatedTick())
            {
                return;
            }

            if (serverTick <= _tickState.GetHashVerifiedTick())
            {
                return;
            }

            if (!_futureCreatedSnapshots.TryGetValue(serverTick, out var snapshots))
            {
                snapshots = new List<TrainUnitSnapshotBundle>();
                _futureCreatedSnapshots.Add(serverTick, snapshots);
            }
            snapshots.Add(snapshot);
        }

        // ダイアグラムイベントを将来tick分だけキューへ積む。
        // Queue diagram events only when their tick is still in the future.
        public void EnqueueDiagram(TrainDiagramEventMessagePack message)
        {
            if (message == null)
            {
                return;
            }

            if (message.Tick <= _tickState.GetSimulatedTick())
            {
                return;
            }

            if (message.Tick <= _tickState.GetHashVerifiedTick())
            {
                return;
            }

            if (!_futureDiagramEvents.TryGetValue(message.Tick, out var messages))
            {
                messages = new List<TrainDiagramEventMessagePack>();
                _futureDiagramEvents.Add(message.Tick, messages);
            }
            messages.Add(message);
        }

        // ハッシュイベントをtick単位でキューへ積む。
        // Queue hash states by tick for tick-aligned verification.
        public void EnqueueHash(TrainUnitHashStateMessagePack message)
        {
            if (message == null)
            {
                return;
            }
            if (message.TrainTick < _tickState.GetSimulatedTick())
            {
                return;
            }

            if (message.TrainTick <= _tickState.GetHashVerifiedTick())
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
            var simulatedTick = _tickState.GetSimulatedTick();
            FlushCreated(simulatedTick);
            FlushDiagram(simulatedTick);

            #region Internal

            void FlushCreated(long currentTick)
            {
                while (TryGetFirstTick(_futureCreatedSnapshots, out var targetTick) && targetTick <= currentTick)
                {
                    var snapshots = _futureCreatedSnapshots[targetTick];
                    for (var i = 0; i < snapshots.Count; i++)
                    {
                        ApplyCreated(snapshots[i], targetTick);
                    }
                    _futureCreatedSnapshots.Remove(targetTick);
                }
            }

            void FlushDiagram(long currentTick)
            {
                while (TryGetFirstTick(_futureDiagramEvents, out var targetTick) && targetTick <= currentTick)
                {
                    var messages = _futureDiagramEvents[targetTick];
                    for (var i = 0; i < messages.Count; i++)
                    {
                        _cache.ApplyDiagramEvent(messages[i]);
                    }
                    _futureDiagramEvents.Remove(targetTick);
                }
            }

            #endregion
        }

        // スナップショットで上書き済みtick以下のイベントを破棄する。
        // Discard queued events at or below the tick already covered by snapshot.
        public void DiscardUpToTick(long tick)
        {
            RemoveUpToTick(_futureCreatedSnapshots, tick);
            RemoveUpToTick(_futureDiagramEvents, tick);
            RemoveUpToTick(_futureHashStates, tick);
        }

        #region Internal

        private static void RemoveUpToTick<T>(SortedDictionary<long, List<T>> source, long tick)
        {
            while (TryGetFirstTick(source, out var targetTick) && targetTick <= tick)
            {
                source.Remove(targetTick);
            }
        }

        private static void RemoveUpToTick<T>(SortedDictionary<long, T> source, long tick)
        {
            while (TryGetFirstTick(source, out var targetTick) && targetTick <= tick)
            {
                source.Remove(targetTick);
            }
        }

        private static bool TryGetFirstTick<T>(SortedDictionary<long, List<T>> source, out long tick)
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

        private static bool TryGetFirstTick<T>(SortedDictionary<long, T> source, out long tick)
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

        private void ApplyCreated(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            // キャッシュ更新と車両オブジェクト更新を同時に適用する。
            // Apply cache update and train-car-object update together.
            _cache.Upsert(snapshot, serverTick);
            _trainCarDatastore.OnTrainObjectUpdate(snapshot.Simulation.Cars);
        }

        #endregion
    }
}
