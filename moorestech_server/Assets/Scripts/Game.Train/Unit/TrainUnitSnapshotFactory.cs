using System.Collections.Generic;

namespace Game.Train.Unit
{
    public static class TrainUnitSnapshotFactory
    {
        public static TrainUnitSnapshotBundle CreateSnapshot(TrainUnit train)
        {
            var simulation = BuildSimulationSnapshot(train);
            var railPosition = train.RailPosition.CreateSaveSnapshot();
            return new TrainUnitSnapshotBundle(simulation, railPosition);
        }

        private static TrainSimulationSnapshot BuildSimulationSnapshot(TrainUnit train)
        {
            var carSnapshots = new List<TrainCarSnapshot>(train.Cars.Count);
            foreach (var car in train.Cars)
            {
                var weight = car.TrainCarMasterElement.Weight + (car.Container?.GetWeight() ?? 0);
                carSnapshots.Add(new TrainCarSnapshot(car.TrainCarInstanceId, car.TrainCarMasterElement.TrainCarGuid, car.IsFacingForward, weight));
            }

            // 駅参照を持つ停車駅だけを座標として送る
            // Send positions only for stops with a station reference
            var stops = new List<TrainTimetableStopSnapshot>(train.trainDiagram.Entries.Count);
            foreach (var entry in train.trainDiagram.Entries)
            {
                var station = entry.Node.StationRef;
                if (station == null || !station.HasStation) continue;
                stops.Add(new TrainTimetableStopSnapshot(station.StationPosition));
            }

            return new TrainSimulationSnapshot(
                train.TrainUnitInstanceId,
                train.CurrentSpeed,
                train.AccumulatedDistance,
                train.masconLevel,
                train.GetManualBranchSelectionIndex(),
                carSnapshots,
                train.IsAutoRun,
                train.trainDiagram.CurrentIndex,
                stops);
        }

    }
}
