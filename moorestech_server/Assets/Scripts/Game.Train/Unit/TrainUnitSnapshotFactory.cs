using System;
using System.Collections.Generic;
using Game.Train.Diagram;
using UnityEngine;

namespace Game.Train.Unit
{
    public static class TrainUnitSnapshotFactory
    {
        public static TrainUnitSnapshotBundle CreateSnapshot(TrainUnit train)
        {
            var simulation = BuildSimulationSnapshot(train);
            var diagram = BuildDiagramSnapshot(train.trainDiagram);
            var railPosition = train.RailPosition.CreateSaveSnapshot();
            return new TrainUnitSnapshotBundle(simulation, diagram, railPosition);
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
                train.IsAutoRun,
                train.IsDocked,
                carSnapshots);
        }

        private static TrainDiagramSnapshot BuildDiagramSnapshot(TrainDiagram diagram)
        {
            var entries = new List<TrainDiagramEntrySnapshot>(diagram.Entries.Count);
            for (var i = 0; i < diagram.Entries.Count; i++)
            {
                var entry = diagram.Entries[i];
                var destination = entry.Node?.ConnectionDestination;
                if (!destination.HasValue)
                {
                    continue;
                }

                entries.Add(new TrainDiagramEntrySnapshot(
                    entry.entryId,
                    destination.Value,
                    CopyDepartureConditions(entry.DepartureConditionTypes),
                    entry.GetWaitForTicksInitialTicks(),
                    entry.GetWaitForTicksRemainingTicks()));
            }

            var normalizedIndex = diagram.Entries.Count == 0
                ? -1
                : Mathf.Clamp(diagram.CurrentIndex, -1, diagram.Entries.Count - 1);

            return new TrainDiagramSnapshot(normalizedIndex, entries);
        }

        private static IReadOnlyList<TrainDiagram.DepartureConditionType> CopyDepartureConditions(
            IReadOnlyList<TrainDiagram.DepartureConditionType> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return Array.Empty<TrainDiagram.DepartureConditionType>();
            }

            var copied = new TrainDiagram.DepartureConditionType[conditions.Count];
            for (var i = 0; i < conditions.Count; i++)
            {
                copied[i] = conditions[i];
            }
            return copied;
        }
    }
}
