using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Unit.Containers;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util
{
    public sealed class TrainAutoRunTestScenario : IDisposable
    {
        private readonly TrainTestEnvironment _environment;
        private readonly TrainCar _trainCar;
        private readonly StationNodeSet _stationNodes;
        private bool _disposed;

        private TrainAutoRunTestScenario(TrainTestEnvironment environment, TrainUnit train, TrainCar trainCar, StationNodeSet stationNodes)
        {
            _environment = environment;
            Train = train;
            _trainCar = trainCar;
            _stationNodes = stationNodes;
        }

        public TrainUnit Train { get; }
        public TrainCar TrainCar => _trainCar;
        public ItemTrainCarContainer ItemContainer => _trainCar.Container as ItemTrainCarContainer;
        public RailNode StationExitFront => _stationNodes.ExitFront;
        public RailNode StationEntryFront => _stationNodes.EntryFront;
        public RailNode StationExitBack => _stationNodes.ExitBack;
        public RailNode StationEntryBack => _stationNodes.EntryBack;
        public int StationSegmentLength => _stationNodes.SegmentLength;

        public RailNode AddConnectedDestinationStation()
        {
            var (_, components) = TrainTestHelper.PlaceBlockWithRailComponents(
                _environment,
                ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(0, 0, 100),
                BlockDirection.North);
            var destination = components.SelectMany(component => new[] { component.FrontNode, component.BackNode })
                .First(node => node.StationRef.NodeRole == StationNodeRole.Exit && node.StationRef.NodeSide == StationNodeSide.Front);
            var entry = components.SelectMany(component => new[] { component.FrontNode, component.BackNode })
                .First(node => node.StationRef.NodeRole == StationNodeRole.Entry && node.StationRef.NodeSide == StationNodeSide.Front);

            // 停車中の駅から別駅への順方向経路を追加する
            // Add a forward route from the docked station to another station
            StationExitFront.ConnectNode(entry, 123456);
            return destination;
        }

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
            var environment = TrainTestHelper.CreateEnvironment();

            var (stationBlock, stationComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                environment,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                Vector3Int.zero,
                BlockDirection.North);
            var (_, r0Component) = TrainTestHelper.PlaceBlockWithComponent<RailComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(12, 34, 56),
                BlockDirection.North);
            var n0 = r0Component.BackNode;
            var (_, r1Component) = TrainTestHelper.PlaceBlockWithComponent<RailComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(-65, 32, -10),
                BlockDirection.South);
            var n1 = r1Component.FrontNode;
            var (_, r2Component) = TrainTestHelper.PlaceBlockWithComponent<RailComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainRail,
                new Vector3Int(-64, 32, -10),
                BlockDirection.South);
            var n2 = r2Component.FrontNode;

            Assert.IsNotNull(stationBlock, "Station block is missing");
            Assert.IsNotNull(stationComponents, "RailComponent list is missing");
            Assert.IsNotNull(n0, "node0 is missing");
            Assert.IsNotNull(n1, "node1 is missing");
            Assert.IsNotNull(n2, "node2 is missing");

            var stationBlockLength = stationBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(stationBlockLength, 0, "Station block size Z must be positive");

            var stationNodes = TrainAutoRunStationNodeResolver.ExtractStationNodes(stationBlock, stationComponents);

            n0.ConnectNode(stationNodes.EntryFront,9876543);
            stationNodes.ExitFront.ConnectNode(n1, 123456);
            n1.ConnectNode(n2, 234567);
            //n0->start->n1->n2 : n2が終端でどこにも繋がらない

            var initialRailNodes = new List<IRailNode> { stationNodes.ExitFront, stationNodes.EntryFront, n0 };
            var initialDistance = startRunning ? stationNodes.SegmentLength - 1 : 0;
            var railPosition = new RailPosition(initialRailNodes, stationNodes.SegmentLength, initialDistance);

            var (trainCar, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, stationNodes.BlockLength, true, TrainTestCarFactory.StableAutoRunTestWeight);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);
            
            trainUnit.trainDiagram.AddEntry(stationNodes.ExitFront);
            trainUnit.trainDiagram.AddEntry(n1);
            trainUnit.trainDiagram.AddEntry(n2);
            trainUnit.trainDiagram.AddEntry(n0);

            var activeEntry = trainUnit.trainDiagram.Entries[0];
            Assert.AreSame(stationNodes.ExitFront, activeEntry.Node,
                "Initial diagram entry should be the station exit node.");
            Assert.AreSame(stationNodes.ExitFront, trainUnit.trainDiagram.GetCurrentNode(),
                "Initial diagram entry should be the station exit node.");
            Assert.AreSame(stationNodes.ExitFront, trainUnit.RailPosition.GetNodeApproaching(),
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

            var updateIterations = startRunning ? Mathf.Max(16, stationNodes.SegmentLength * 8) : 1;
            var undocked = !startRunning;
            for (var i = 0; i < updateIterations; i++)
            {
                trainUnit.Update();
                if (startRunning && !trainUnit.trainUnitStationDocking.IsDocked)
                {
                    undocked = true;
                    break;
                }
            }

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "Docked scenario should leave the train docked at the station.");
            }
            else
            {
                Assert.IsTrue(undocked, "Running scenario should represent a train that has departed the station.");
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "Running scenario should represent a train that has departed the station.");
                Assert.AreSame(stationNodes.ExitFront, trainUnit.trainDiagram.GetCurrentNode(),
                    "Train should be heading towards the next station.");
            }

            return new TrainAutoRunTestScenario(environment, trainUnit, trainCar, stationNodes);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Train.trainUnitStationDocking.UndockFromStation();
            _environment.GetTrainDiagramManager().UnregisterDiagram(Train.trainDiagram);
            _environment.GetITrainUnitMutationDatastore().UnregisterTrain(Train);
            _disposed = true;
        }


    }
}
