using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Train.Unit
{
    public class TrainUnitDatastore : ITrainUnitMutationDatastore, ITrainUnitLookupDatastore
    {
        private readonly Dictionary<TrainUnitInstanceId, TrainUnit> _trainUnitsById = new();
        private readonly Dictionary<TrainCarInstanceId, TrainUnit> _trainUnitsByCarId = new();
        private readonly Dictionary<TrainCarInstanceId, TrainCar> _trainCarsById = new();

        public IReadOnlyCollection<TrainUnit> GetRegisteredTrains() => _trainUnitsById.Values;

        public void RegisterTrain(TrainUnit trainUnit)
        {
            if (trainUnit == null || trainUnit.TrainUnitInstanceId == TrainUnitInstanceId.Empty)
            {
                return;
            }

            // 列車本体の正本を登録し、派生 index を張り直す
            // Register the source-of-truth train unit and rebuild derived indexes.
            _trainUnitsById[trainUnit.TrainUnitInstanceId] = trainUnit;
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
            _trainUnitsById.Remove(trainUnit.TrainUnitInstanceId);
            RebuildCarToUnitIndex();
        }

        public bool TryGetTrainUnit(TrainUnitInstanceId id, out TrainUnit trainUnit)
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

            // 逆引きを構築する前に、全列車を跨いで車両IDが一意であることを保証する
            // Ensure car ids are unique across every train before building the reverse lookup.
            var allCars = _trainUnitsById.Values
                .Where(unit => unit?.Cars != null)
                .SelectMany(unit => unit.Cars)
                .Where(car => car != null)
                .ToList();
            EnsureTrainCarInstanceIdsUnique(allCars.Select(car => car.TrainCarInstanceId));

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

                    // 車両参照と所属TrainUnit参照を同時に更新する
                    // Rebuild direct car lookup and owning TrainUnit lookup together.
                    _trainUnitsByCarId[car.TrainCarInstanceId] = trainUnit;
                    _trainCarsById[car.TrainCarInstanceId] = car;
                }
            }
        }

        // 列車車両IDの重複を検出し、重複があれば例外を投げる（壊れたセーブや採番衝突の検出用）
        // Detects duplicate train car ids and throws; guards against corrupted saves and id collisions.
        public static void EnsureTrainCarInstanceIdsUnique(IEnumerable<TrainCarInstanceId> ids)
        {
            var seen = new HashSet<TrainCarInstanceId>();
            foreach (var id in ids)
            {
                if (!seen.Add(id))
                {
                    throw new Exception($"Duplicate TrainCarInstanceId: {id}");
                }
            }
        }

        public void Reset()
        {
            _trainUnitsById.Clear();
            _trainUnitsByCarId.Clear();
            _trainCarsById.Clear();
        }
    }
}
