using System;
using System.Collections.Generic;
using Game.Train.Unit;
using UniRx;

namespace Game.Train.Event
{
    // TrainUnit構造変更の通知を集約する
    // Aggregate notifications for train-unit structure changes.
    public sealed class TrainUnitSnapshotNotifyEvent : ITrainUnitSnapshotNotifyEvent
    {
        private readonly Subject<TrainUnitSnapshotNotifyEventData> _subject = new();
        public IObservable<TrainUnitSnapshotNotifyEventData> OnTrainUnitSnapshotNotified => _subject;

        public void NotifySnapshot(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 単機スナップショットの更新を通知する
            // Notify that a single train unit snapshot should be updated.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainUnit.TrainInstanceId, false, trainUnit));
        }

        public void NotifyDeleted(TrainInstanceId trainInstanceId)
        {
            if (trainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 編成削除通知を発行する
            // Notify that a train unit has been deleted.
            _subject.OnNext(new TrainUnitSnapshotNotifyEventData(trainInstanceId, true, null));
        }

        public void NotifyChangedByBeforeAfter(IReadOnlyList<TrainUnit> beforeTrains, IReadOnlyList<TrainUnit> afterTrains)
        {
            // 変更前後の車両構成差分から通知対象編成を抽出する
            // Extract changed train units by comparing before/after car composition.
            var previousState = BuildTrainState(beforeTrains);
            var latestState = BuildTrainState(afterTrains);
            var latestTrainById = BuildTrainById(afterTrains);
            var changedTrainIds = CollectChangedTrainIds(previousState, latestState);

            foreach (var trainInstanceId in changedTrainIds)
            {
                if (latestTrainById.TryGetValue(trainInstanceId, out var updatedTrain))
                {
                    NotifySnapshot(updatedTrain);
                    continue;
                }
                NotifyDeleted(trainInstanceId);
            }
            return;

            #region Internal

            static Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> BuildTrainState(IReadOnlyList<TrainUnit> trains)
            {
                // 編成ごとの車両ID集合を作る
                // Build a set of car ids per train unit.
                var state = new Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>>();
                if (trains == null)
                {
                    return state;
                }
                for (var i = 0; i < trains.Count; i++)
                {
                    var train = trains[i];
                    if (train == null || train.TrainInstanceId == TrainInstanceId.Empty)
                    {
                        continue;
                    }

                    var carIds = new HashSet<TrainCarInstanceId>();
                    for (var j = 0; j < train.Cars.Count; j++)
                    {
                        carIds.Add(train.Cars[j].TrainCarInstanceId);
                    }
                    state[train.TrainInstanceId] = carIds;
                }
                return state;
            }

            static Dictionary<TrainInstanceId, TrainUnit> BuildTrainById(IReadOnlyList<TrainUnit> trains)
            {
                // 最新編成をIDで引ける辞書にする
                // Build an id -> train lookup table for latest units.
                var result = new Dictionary<TrainInstanceId, TrainUnit>();
                if (trains == null)
                {
                    return result;
                }
                for (var i = 0; i < trains.Count; i++)
                {
                    var train = trains[i];
                    if (train == null || train.TrainInstanceId == TrainInstanceId.Empty)
                    {
                        continue;
                    }
                    result[train.TrainInstanceId] = train;
                }
                return result;
            }

            static HashSet<TrainInstanceId> CollectChangedTrainIds(
                Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> previousState,
                Dictionary<TrainInstanceId, HashSet<TrainCarInstanceId>> latestState)
            {
                // 追加/削除/車両構成変化をすべて変更対象にする
                // Treat add/remove/car-set changes as changed train units.
                var changed = new HashSet<TrainInstanceId>();
                foreach (var previousEntry in previousState)
                {
                    if (!latestState.ContainsKey(previousEntry.Key))
                    {
                        changed.Add(previousEntry.Key);
                    }
                }

                foreach (var latestEntry in latestState)
                {
                    if (!previousState.TryGetValue(latestEntry.Key, out var previousCars) || !previousCars.SetEquals(latestEntry.Value))
                    {
                        changed.Add(latestEntry.Key);
                    }
                }
                return changed;
            }

            #endregion
        }
    }
}
