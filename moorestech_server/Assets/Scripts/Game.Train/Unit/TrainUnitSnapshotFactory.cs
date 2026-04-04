using System;
using System.Collections.Generic;
using Game.Train.Unit.Containers;

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
                carSnapshots.Add(new TrainCarSnapshot(car.TrainCarInstanceId, car.TrainCarMasterElement.TrainCarGuid, car.IsFacingForward, car.RemainFuelTime > 0, weight));
            }

            return new TrainSimulationSnapshot(
                train.TrainInstanceId,
                train.CurrentSpeed,
                train.AccumulatedDistance,
                train.masconLevel,
                carSnapshots);
        }

    }
}
