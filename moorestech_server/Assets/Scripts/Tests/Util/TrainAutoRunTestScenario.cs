using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util
{
    public sealed class TrainAutoRunTestScenario : IDisposable
    {
        private readonly TrainCar _trainCar;
        private readonly RailNode[] _diagramNodes;
        private readonly StationNodeSet _stationNodes;
        private bool _disposed;

        private TrainAutoRunTestScenario(
            TrainUnit train,
            TrainCar trainCar,
            StationNodeSet stationNodes,
            RailNode[] diagramNodes)
        {
            Train = train;
            _trainCar = trainCar;
            _stationNodes = stationNodes;
            _diagramNodes = diagramNodes;
        }

        public TrainUnit Train { get; }
        public TrainCar TrainCar => _trainCar;
        public RailNode StationExitFront => _stationNodes.ExitFront;
        public RailNode StationEntryFront => _stationNodes.EntryFront;
        public RailNode StationExitBack => _stationNodes.ExitBack;
        public RailNode StationEntryBack => _stationNodes.EntryBack;
        public IReadOnlyList<RailNode> DiagramNodes => _diagramNodes;
        public int StationSegmentLength => _stationNodes.SegmentLength;

        public static TrainAutoRunTestScenario CreateDockedScenario()
        {
            return CreateScenario(startRunning: false);
        }

        public static TrainAutoRunTestScenario CreateRunningScenario()
        {
            return CreateScenario(startRunning: true);
        }

        private static TrainAutoRunTestScenario CreateScenario(bool startRunning)
        {
            var environment = TrainTestHelper.CreateEnvironmentWithRailGraph(out _);

            var (_, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                Vector3Int.zero,
                BlockDirection.North);

            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");

            var stationNodes = ExtractStationNodes(railSaver!);
            var diagramNodes = new[]
            {
                stationNodes.ExitFront,
                stationNodes.EntryFront,
                stationNodes.ExitBack
            };

            var initialRailNodes = new List<RailNode> { stationNodes.ExitFront, stationNodes.EntryFront };
            var initialDistance = startRunning ? stationNodes.SegmentLength : 0;
            var railPosition = new RailPosition(initialRailNodes, stationNodes.SegmentLength, initialDistance);

            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: stationNodes.SegmentLength);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            foreach (var node in diagramNodes)
            {
                trainUnit.trainDiagram.AddEntry(node);
            }

            trainUnit.trainDiagram.MoveToNextEntry();

            if (startRunning)
            {
                var activeEntry = trainUnit.trainDiagram.Entries.First(entry => entry.Node == stationNodes.ExitFront);
                activeEntry.SetDepartureConditions(null);
            }

            trainUnit.TurnOnAutoRun();

            var updateCount = startRunning ? 6 : 1;
            for (var i = 0; i < updateCount; i++)
            {
                trainUnit.Update();
            }

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "Docked scenario should leave the train docked at the station.");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should represent a train that has departed the station.");
            }

            return new TrainAutoRunTestScenario(trainUnit, trainCar, stationNodes, diagramNodes);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Train.trainUnitStationDocking.UndockFromStation();
            TrainDiagramManager.Instance.UnregisterDiagram(Train);
            TrainUpdateService.Instance.UnregisterTrain(Train);
            _disposed = true;
        }

        private readonly struct StationNodeSet
        {
            public StationNodeSet(
                RailNode exitFront,
                RailNode entryFront,
                RailNode exitBack,
                RailNode entryBack,
                int segmentLength)
            {
                ExitFront = exitFront;
                EntryFront = entryFront;
                ExitBack = exitBack;
                EntryBack = entryBack;
                SegmentLength = segmentLength;
            }

            public RailNode ExitFront { get; }
            public RailNode EntryFront { get; }
            public RailNode ExitBack { get; }
            public RailNode EntryBack { get; }
            public int SegmentLength { get; }
        }

        private static StationNodeSet ExtractStationNodes(RailSaverComponent railSaver)
        {
            var nodeInfos = railSaver.RailComponents
                .SelectMany(component => new[]
                {
                    (Node: component.FrontNode, IsFront: true),
                    (Node: component.BackNode, IsFront: false)
                })
                .Where(info => info.Node != null)
                .ToList();

            var exitFront = nodeInfos
                .FirstOrDefault(info => info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Exit)
                .Node;
            Assert.IsNotNull(exitFront, "Station exit (front) node not found");

            var entryFront = nodeInfos
                .FirstOrDefault(info => info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Entry)
                .Node;
            Assert.IsNotNull(entryFront, "Station entry (front) node not found");

            var exitBack = nodeInfos
                .FirstOrDefault(info => !info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Exit)
                .Node;
            Assert.IsNotNull(exitBack, "Station exit (back) node not found");

            var entryBack = nodeInfos
                .FirstOrDefault(info => !info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Entry)
                .Node;
            Assert.IsNotNull(entryBack, "Station entry (back) node not found");

            var segmentLength = exitFront!.GetDistanceToNode(entryFront!);
            Assert.Greater(segmentLength, 0, "Station segment length must be positive");

            return new StationNodeSet(exitFront!, entryFront!, exitBack!, entryBack!, segmentLength);
        }
    }
}
