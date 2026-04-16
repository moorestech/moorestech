using System;
using System.Collections.Generic;

namespace Game.Train.Unit
{
    public class TrainUnitDatastore : ITrainUnitMutationDatastore, ITrainUnitLookupDatastore
    {
        private readonly Dictionary<TrainInstanceId, TrainUnit> _trainUnitsById = new();
        private readonly Dictionary<TrainCarInstanceId, TrainUnit> _trainUnitsByCarId = new();
        private readonly Dictionary<TrainCarInstanceId, TrainCar> _trainCarsById = new();

        public event Action<TrainInstanceId> TrainRemoved;

        public IReadOnlyCollection<TrainUnit> GetRegisteredTrains() => _trainUnitsById.Values;

        public void RegisterTrain(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 列車本体を正とし、派生 index は都度再構築する
            // Keep TrainUnit as the source of truth and rebuild derived indexes
            _trainUnitsById[trainUnit.TrainInstanceId] = trainUnit;
            RebuildCarToUnitIndex();
        }

        public void UnregisterTrain(TrainUnit trainUnit)
        {
            if (trainUnit == null)
            {
                return;
            }

            // unregister 時に manual input の残骸も同時に掃除する
            // Clear manual control state together with the train unregister
            NotifyTrainRemoved(trainUnit.TrainInstanceId);
            _trainUnitsById.Remove(trainUnit.TrainInstanceId);
            RebuildCarToUnitIndex();
        }

        public bool TryGetTrainUnit(TrainInstanceId id, out TrainUnit trainUnit)
        {
            return _trainUnitsById.TryGetValue(id, out trainUnit);
        }

        public bool TryGetTrainUnitByCar(TrainCarInstanceId id, out TrainUnit trainUnit)
        {
            return _trainUnitsByCarId.TryGetValue(id, out trainUnit);
        }

        public bool TryGetTrainCar(TrainCarInstanceId id, out TrainCar trainCar)
        {
            return _trainCarsById.TryGetValue(id, out trainCar);
        }

        public void RebuildCarToUnitIndex()
        {
            _trainUnitsByCarId.Clear();
            _trainCarsById.Clear();

            // TrainUnit -> Cars を正本として逆引き index を張り直す
            // Rebuild reverse lookups from the authoritative TrainUnit -> Cars state
            foreach (var trainUnit in _trainUnitsById.Values)
            {
                if (trainUnit?.Cars == null)
                {
                    continue;
                }

                foreach (var car in trainUnit.Cars)
                {
                    if (car == null)
                    {
                        continue;
                    }

                    // car 本体 lookup と owner train lookup を同時に更新する
                    // Rebuild direct car lookup and owning TrainUnit lookup together
                    _trainUnitsByCarId[car.TrainCarInstanceId] = trainUnit;
                    _trainCarsById[car.TrainCarInstanceId] = car;
                }
            }
        }

        public void Reset()
        {
            // テストや再初期化時に manual input の残留も防ぐ
            // Clear train-linked manual state during datastore reset as well
            foreach (var trainId in _trainUnitsById.Keys)
            {
                NotifyTrainRemoved(trainId);
            }

            _trainUnitsById.Clear();
            _trainUnitsByCarId.Clear();
            _trainCarsById.Clear();
        }

        private void NotifyTrainRemoved(TrainInstanceId trainId)
        {
            if (trainId == TrainInstanceId.Empty)
            {
                return;
            }

            TrainRemoved?.Invoke(trainId);
        }
    }
}
