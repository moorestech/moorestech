using System.Collections.Generic;

namespace Game.Train.Unit
{
    public class TrainUnitDatastore : ITrainUnitMutationDatastore, ITrainUnitLookupDatastore
    {
        private readonly Dictionary<TrainInstanceId, TrainUnit> _trainUnitsById = new();
        private readonly Dictionary<TrainCarInstanceId, TrainUnit> _trainUnitsByCarId = new();

        public IReadOnlyCollection<TrainUnit> GetRegisteredTrains() => _trainUnitsById.Values;

        public void RegisterTrain(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainInstanceId == TrainInstanceId.Empty)
            {
                return;
            }

            // 列車本体の正本を登録し、派生 index を張り直す
            // Register the source-of-truth train unit and rebuild derived indexes.
            _trainUnitsById[trainUnit.TrainInstanceId] = trainUnit;
            RebuildCarToUnitIndex();
        }

        public void UnregisterTrain(TrainUnit trainUnit)
        {
            if (trainUnit == null)
            {
                return;
            }

            // 列車登録を外したら car -> unit も必ず再計算する
            // Recompute car -> unit after removing a registered train.
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

        public void RebuildCarToUnitIndex()
        {
            _trainUnitsByCarId.Clear();

            // TrainUnit -> Cars を正本として、逆引き index を全再構築する
            // Rebuild the reverse lookup from the authoritative TrainUnit -> Cars state.
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

                    _trainUnitsByCarId[car.TrainCarInstanceId] = trainUnit;
                }
            }
        }

        public void Clear()
        {
            _trainUnitsById.Clear();
            _trainUnitsByCarId.Clear();
        }
    }
}
