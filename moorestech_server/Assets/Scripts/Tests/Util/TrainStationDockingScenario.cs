using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util
{
    public sealed class TrainStationDockingScenario : IDisposable
    {
        private readonly TrainTestEnvironment _environment;
        private readonly RailComponent _n0Component;
        private readonly RailComponent _n1Component;
        private readonly RailComponent _n2Component;
        private readonly RailComponent _n3Component;
        private readonly StationNodeSet _station;
        private readonly List<TrainUnit> _spawnedTrains = new();
        private bool _disposed;

        private TrainStationDockingScenario(
            TrainTestEnvironment environment,
            RailComponent n0Component,
            RailComponent n1Component,
            StationNodeSet station,
            RailComponent n2Component,
            RailComponent n3Component)
        {
            _environment = environment;
            _n0Component = n0Component;
            _n1Component = n1Component;
            _station = station;
            _n2Component = n2Component;
            _n3Component = n3Component;
        }

        public TrainTestEnvironment Environment => _environment;
        public int StationSegmentLength => _station.SegmentLength;
        public RailNode StationExitFront => _station.ExitFront;
        public RailNode StationEntryFront => _station.EntryFront;
        public RailNode StationExitBack => _station.ExitBack;
        public RailNode StationEntryBack => _station.EntryBack;
        public Vector3Int StationBlockPosition => _station.EntryComponent.ComponentID.Position;

        public static TrainStationDockingScenario Create()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            var n0Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var n1Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(5, 0, 0), BlockDirection.North);
            var (_, stationSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(10, 20, 0),
                BlockDirection.North);
            var n2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(15, 0, 0), BlockDirection.North);
            var n3Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(20, 0, 0), BlockDirection.North);

            Assert.IsNotNull(stationSaver, "貨物プラットフォーム用のRailSaverComponentを取得できませんでした。");

            var station = ExtractStationNodes(stationSaver!);

            n0Component.ConnectRailComponent(n1Component, true, true);
            n1Component.ConnectRailComponent(station.EntryComponent, true, true);
            station.ExitComponent.ConnectRailComponent(n2Component, true, true);
            n2Component.ConnectRailComponent(n3Component, true, true);

            return new TrainStationDockingScenario(environment, n0Component, n1Component, station, n2Component, n3Component);
        }

        public static TrainStationDockingScenario CreateWithLoop()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            var n0Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var n1Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(5, 0, 0), BlockDirection.North);
            var n2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(15, 0, 0), BlockDirection.North);
            var n3Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(20, 0, 0), BlockDirection.North);
            var (_, stationSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(10, 100, 0),
                BlockDirection.North);

            Assert.IsNotNull(stationSaver, "貨物プラットフォーム用のRailSaverComponentを取得できませんでした。");

            var station = ExtractStationNodes(stationSaver!);

            n0Component.ConnectRailComponent(station.EntryComponent, true, true, station.SegmentLength);
            station.ExitComponent.ConnectRailComponent(n0Component, true, true, station.SegmentLength * 2);

            return new TrainStationDockingScenario(environment, n0Component, n1Component, station, n2Component, n3Component);
        }

        public TrainUnit CreateForwardDockingTrain(out TrainCar car, int initialDistanceToExit = 0)
        {
            var nodes = new List<RailNode>
            {
                _station.ExitFront,
                _station.EntryFront,
                _n1Component.FrontNode,
                _n0Component.FrontNode
            };

            return CreateTrain(nodes, initialDistanceToExit, out car);
        }

        public TrainUnit CreateOpposingDockingTrain(out TrainCar car, int initialDistanceToExit = 0)
        {
            var nodes = new List<RailNode>
            {
                _station.ExitBack,
                _station.EntryBack,
                _n2Component.BackNode,
                _n3Component.BackNode
            };

            return CreateTrain(nodes, initialDistanceToExit, out car, false);
        }

        public TrainUnit CreateLoopDockingTrain(int carCount, out IReadOnlyList<TrainCar> cars)
        {
            Assert.GreaterOrEqual(carCount, 16, "超長編成のテストには16両以上の車両数を指定してください。");

            var requiredLength = carCount * _station.SegmentLength;
            var nodes = BuildLoopRailNodes(requiredLength);

            var trainCars = new List<TrainCar>(carCount);
            for (var i = 0; i < carCount; i++)
            {
                var master = new TrainCarMasterElement(Guid.Empty, Guid.Empty, null, 1000, 1, _station.SegmentLength);
                trainCars.Add(new TrainCar(master));
            }

            var train = CreateTrain(nodes, trainCars, 0);
            cars = trainCars;
            return train;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var train in _spawnedTrains)
            {
                if (train.TrainId == Guid.Empty) continue;
                if (train.trainUnitStationDocking.IsDocked)
                {
                    train.trainUnitStationDocking.UndockFromStation();
                }
                TrainDiagramManager.Instance.UnregisterDiagram(train.trainDiagram);
                TrainUpdateService.Instance.UnregisterTrain(train);
            }

            _spawnedTrains.Clear();
            _disposed = true;
        }

        private TrainUnit CreateTrain(List<RailNode> nodes, int initialDistanceToNextNode, out TrainCar car, bool isFacingFront = true)
        {
            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();

            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,isFacingFront)
            };

            car = cars[0];
            return CreateTrain(nodes, cars, initialDistanceToNextNode);
        }

        private TrainUnit CreateTrain(List<RailNode> nodes, List<TrainCar> cars, int initialDistanceToNextNode)
        {
            Assert.IsNotNull(nodes, "RailNodeのリストがnullです。");
            Assert.GreaterOrEqual(nodes.Count, 2, "列車の生成には2つ以上のRailNodeが必要です。");

            ValidateNodeOrientations(nodes);

            Assert.GreaterOrEqual(initialDistanceToNextNode, 0, "初期距離は0以上である必要があります。");

            var trainLength = cars.Sum(trainCar => trainCar.Length);
            var railPosition = new RailPosition(nodes, trainLength, initialDistanceToNextNode);
            var train = new TrainUnit(railPosition, cars);
            _spawnedTrains.Add(train);
            return train;
        }

        private List<RailNode> BuildLoopRailNodes(int requiredLength)
        {
            var nodes = new List<RailNode> { };
            var loopSequence = new[]
            {
                _station.ExitFront,
                _station.EntryFront,
                _n0Component.FrontNode,
            };

            while (RailNodeCalculate.CalculateTotalDistance(nodes) < requiredLength)
            {
                nodes.AddRange(loopSequence);
            }

            return nodes;
        }

        private static void ValidateNodeOrientations(IReadOnlyList<RailNode> nodes)
        {
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var nextDistance = nodes[i + 1].GetDistanceToNode(nodes[i]);
                Assert.Greater(nextDistance, 0,
                    $"RailNodeの順序が誤っています。ノード {i + 1} から {i} へ到達できません。");
            }
        }

        private static StationNodeSet ExtractStationNodes(RailSaverComponent stationSaver)
        {
            var entryComponent = stationSaver.RailComponents
                .FirstOrDefault(component =>
                    component.FrontNode.StationRef.NodeRole == StationNodeRole.Entry &&
                    component.FrontNode.StationRef.NodeSide == StationNodeSide.Front);
            Assert.IsNotNull(entryComponent, "駅の正面Entryノードを持つRailComponentが見つかりません。");

            var exitComponent = stationSaver.RailComponents
                .FirstOrDefault(component =>
                    component.FrontNode.StationRef.NodeRole == StationNodeRole.Exit &&
                    component.FrontNode.StationRef.NodeSide == StationNodeSide.Front);
            Assert.IsNotNull(exitComponent, "駅の正面Exitノードを持つRailComponentが見つかりません。");

            var entryFront = entryComponent!.FrontNode;
            var exitFront = exitComponent!.FrontNode;

            var entryBack = exitComponent.BackNode;
            Assert.AreEqual(StationNodeRole.Entry, entryBack.StationRef.NodeRole,
                "Exit側RailComponentの背面ノードがEntryとして設定されていません。");
            Assert.AreEqual(StationNodeSide.Back, entryBack.StationRef.NodeSide,
                "Exit側RailComponentの背面ノードがBack側として設定されていません。");

            var exitBack = entryComponent.BackNode;
            Assert.AreEqual(StationNodeRole.Exit, exitBack.StationRef.NodeRole,
                "Entry側RailComponentの背面ノードがExitとして設定されていません。");
            Assert.AreEqual(StationNodeSide.Back, exitBack.StationRef.NodeSide,
                "Entry側RailComponentの背面ノードがBack側として設定されていません。");

            var segmentLength = entryFront.GetDistanceToNode(exitFront);
            Assert.Greater(segmentLength, 0, "駅セグメントの長さが0以下になっています。");

            var backSegmentLength = entryBack.GetDistanceToNode(exitBack);
            Assert.Greater(backSegmentLength, 0, "背面側の駅セグメントの長さが0以下になっています。");

            return new StationNodeSet(entryComponent, exitComponent, entryFront, exitFront, entryBack, exitBack, segmentLength);
        }

        private readonly struct StationNodeSet
        {
            public StationNodeSet(
                RailComponent entryComponent,
                RailComponent exitComponent,
                RailNode entryFront,
                RailNode exitFront,
                RailNode entryBack,
                RailNode exitBack,
                int segmentLength)
            {
                EntryComponent = entryComponent;
                ExitComponent = exitComponent;
                EntryFront = entryFront;
                ExitFront = exitFront;
                EntryBack = entryBack;
                ExitBack = exitBack;
                SegmentLength = segmentLength;
            }

            public RailComponent EntryComponent { get; }
            public RailComponent ExitComponent { get; }
            public RailNode EntryFront { get; }
            public RailNode ExitFront { get; }
            public RailNode EntryBack { get; }
            public RailNode ExitBack { get; }
            public int SegmentLength { get; }
        }
    }
}
