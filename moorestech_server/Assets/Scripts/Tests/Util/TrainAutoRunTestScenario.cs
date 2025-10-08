using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Tests.Module.TestMod;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Tests.Util
{
    public sealed class TrainAutoRunTestScenario : IDisposable
    {
        private readonly TrainCar _trainCar;
        private readonly StationNodeSet _stationNodes;
        private bool _disposed;

        private TrainAutoRunTestScenario(
            TrainUnit train,
            TrainCar trainCar,
            StationNodeSet stationNodes)
        {
            Train = train;
            _trainCar = trainCar;
            _stationNodes = stationNodes;
        }

        public TrainUnit Train { get; }
        public TrainCar TrainCar => _trainCar;
        public RailNode StationExitFront => _stationNodes.ExitFront;
        public RailNode StationEntryFront => _stationNodes.EntryFront;
        public RailNode StationExitBack => _stationNodes.ExitBack;
        public RailNode StationEntryBack => _stationNodes.EntryBack;
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
            var (_, r0Saver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(12, 34, 56),
                BlockDirection.North);
            var n0 = r0Saver.RailComponents[0].BackNode;
            var (_, r1Saver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(-65, 32, -10),
                BlockDirection.South);
            var n1 = r1Saver.RailComponents[0].FrontNode;
            var (_, r2Saver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(-65, 32, -10),
                BlockDirection.South);
            var n2 = r2Saver.RailComponents[0].FrontNode;

            Assert.IsNotNull(railSaver, "RailSaverComponent is missing");
            Assert.IsNotNull(n0, "node0 is missing");
            Assert.IsNotNull(n1, "node1 is missing");
            Assert.IsNotNull(n2, "node2 is missing");

            var stationNodes = ExtractStationNodes(railSaver!);

            n0.ConnectNode(stationNodes.EntryFront,9876543);
            stationNodes.ExitFront.ConnectNode(n1, 123456);
            n1.ConnectNode(n2, 234567);
            //n0->start->n1->n2 : n2が終端でどこにも繋がらない

            var initialRailNodes = new List<RailNode> { stationNodes.ExitFront, stationNodes.EntryFront, n0 };
            var initialDistance = startRunning ? stationNodes.SegmentLength - 1 : 0;
            var railPosition = new RailPosition(initialRailNodes, stationNodes.SegmentLength, initialDistance);

            var trainCar = new TrainCar(tractionForce: 1000, inventorySlots: 1, length: stationNodes.SegmentLength);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            trainUnit.trainDiagram.AddEntry(stationNodes.ExitFront);
            trainUnit.trainDiagram.AddEntry(n1);
            trainUnit.trainDiagram.AddEntry(n2);
            trainUnit.trainDiagram.AddEntry(n0);

            var activeEntry = trainUnit.trainDiagram.Entries[0];
            Assert.AreSame(stationNodes.ExitFront, activeEntry.Node,
                "Initial diagram entry should be the station exit node.");
            Assert.AreSame(stationNodes.ExitFront, trainUnit.trainDiagram.GetNextDestination(),
                "Initial diagram entry should be the station exit node.");
            Assert.AreSame(stationNodes.ExitFront, trainUnit._railPosition.GetNodeApproaching(),
                "Initial diagram entry should be the station exit node.");

            if (startRunning)
            {
                activeEntry.SetDepartureConditions(null);
            }
            else
            {
                activeEntry.SetDepartureWaitTicks(400);
            }

            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun, "Train should be in auto-run mode.");

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
                Assert.AreSame(stationNodes.ExitFront, trainUnit.trainDiagram.GetNextDestination(),
                    "Train should be heading towards the next station.");
            }

            return new TrainAutoRunTestScenario(trainUnit, trainCar, stationNodes);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Train.trainUnitStationDocking.UndockFromStation();
            TrainDiagramManager.Instance.UnregisterDiagram(Train.trainDiagram);
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

            var segmentLength = entryFront!.GetDistanceToNode(exitFront!);
            Assert.Greater(segmentLength, 0, "Station segment length must be positive");

            return new StationNodeSet(exitFront!, entryFront!, exitBack!, entryBack!, segmentLength);
        }
    }
}
