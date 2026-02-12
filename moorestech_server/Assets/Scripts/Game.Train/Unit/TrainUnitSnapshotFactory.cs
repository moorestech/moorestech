using System;
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
                carSnapshots.Add(new TrainCarSnapshot(car.TrainCarInstanceId, car.TrainCarMasterElement.TrainCarGuid, car.InventorySlots, car.TractionForce, car.IsFacingForward));
            }

            return new TrainSimulationSnapshot(
                train.TrainId,
                train.CurrentSpeed,
                train.AccumulatedDistance,
                train.masconLevel,
                carSnapshots);
        }

    }
}
