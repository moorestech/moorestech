using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Extension;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class TrainHugeAutoRunTrainSaveLoadConsistencyTest
    {
        private const int RailComponentCount = 140;//2200
        private const int DiagramNodeSelectionCount = 99;//8999
        private const int TrainCount = 65;//65
        private const int TotalTicks = 9611;//73611
        private const int SaveAfterTicks = 6003;//50003
        private const int MaxDiagramEntries = 17;//17
        private const int MinDiagramEntries = 1;//1
        private const int MaxWaitTicks = 96;//4097
        private const int RandomSeed = 3572468;//3572468

        /*
        private const int RailComponentCount = 6201;
        private const int DiagramNodeSelectionCount = 8999;
        private const int TrainCount = 165;
        private const int TotalTicks = 273611;
        private const int SaveAfterTicks = 90003;
        private const int MaxDiagramEntries = 17;
        private const int MinDiagramEntries = 1;
        private const int MaxWaitTicks = 4097;
        private const int RandomSeed = 3572466;
        */

        [Timeout(500000)]
        [Test]
        public void MassiveAutoRunScenarioProducesIdenticalStateWithAndWithoutSaveLoad()
        {
            var baselineSnapshots = RunScenarioWithoutSave(RandomSeed, TotalTicks, SaveAfterTicks);
            var saveLoadSnapshots = RunScenarioWithSave(RandomSeed, TotalTicks, SaveAfterTicks);

            Assert.AreEqual(baselineSnapshots.Count, saveLoadSnapshots.Count,
                "列車数が一致しません。");

            foreach (var (length, baseline) in baselineSnapshots)
            {
                Assert.IsTrue(saveLoadSnapshots.TryGetValue(length, out var afterSaveLoad),
                    $"列車長{length}の列車がセーブ＆ロード後に見つかりません。");
                baseline.AssertEqual(afterSaveLoad);
            }
        }

        private static Dictionary<int, TrainSimulationSnapshot> RunScenarioWithoutSave(int seed, int totalTicks, int saveAfterTicks)
        {
            var (scenario, _) = SetupScenario(seed);
            AdvanceTicks(totalTicks - saveAfterTicks);
            AdvanceTicks(saveAfterTicks);

            var snapshots = CaptureSnapshots();

            CleanupTrains();
            CleanupWorld(scenario.Environment);

            return snapshots;
        }

        private static Dictionary<int, TrainSimulationSnapshot> RunScenarioWithSave(int seed, int totalTicks, int saveAfterTicks)
        {
            RailGraphDatastore.ResetInstance();
            var (scenario, expectedSnapshot) = SetupScenario(seed);

            AdvanceTicks(totalTicks - saveAfterTicks);

            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(scenario.Environment.ServiceProvider);
            
            var preSaveTrains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            
            foreach (var train in preSaveTrains)
            {
                train.OnDestroy();
            }
            TrainUpdateService.Instance.ResetTrains();
            CleanupWorld(scenario.Environment);
            RailGraphDatastore.ResetInstance();

            var loadEnvironment = TrainTestHelper.CreateEnvironment();
            SetupRandomLengthTrainCarMasters(seed, TrainCount, 600000);
            SaveLoadJsonTestHelper.LoadFromJson(loadEnvironment.ServiceProvider, saveJson);

            // RailGraph再構築のチェック
            var loadedComponents = new List<RailComponent>();
            UnityEngine.Random.InitState(seed);

            var xList = ReturnShuffleList(RailComponentCount);
            var yList = ReturnShuffleList(RailComponentCount);
            var zList = ReturnShuffleList(RailComponentCount);
            var directions = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            var positions = new List<Vector3Int>();
            for (var i = 0; i < RailComponentCount; i++)
            {
                var position = new Vector3Int(xList[i], yList[i], zList[i]);
                positions.Add(position);
            }
            foreach (var position in positions)
            {
                var block = loadEnvironment.WorldBlockDatastore.GetBlock(position);
                Assert.IsNotNull(block, $"座標 {position} にレールブロックがロードされていません。");

                var saverComponent = block.GetComponent<RailSaverComponent>();
                Assert.IsNotNull(saverComponent, $"座標 {position} のRailSaverComponentを取得できませんでした。");
                Assert.IsNotEmpty(saverComponent.RailComponents,
                    $"座標 {position} のRailSaverComponentにRailComponentが含まれていません。");
                loadedComponents.Add(saverComponent.RailComponents[0]);
            }

            var actualSnapshot = RailGraphNetworkTestHelper.CaptureFromComponents(loadedComponents);
            RailGraphNetworkTestHelper.AssertEquivalent(expectedSnapshot, actualSnapshot);

            AdvanceTicks(saveAfterTicks);
            var snapshots = CaptureSnapshots();
            CleanupTrains();
            CleanupWorld(loadEnvironment);
            return snapshots;
        }

        private static (ScenarioSetup, RailGraphNetworkSnapshot) SetupScenario(int seed)
        {
            UnityEngine.Random.InitState(seed);
            var environment = TrainTestHelper.CreateEnvironment();
            SetupRandomLengthTrainCarMasters(seed, TrainCount, 600000);
            TrainUpdateService.Instance.ResetTrains();

            var components = BuildRailNetwork(environment, RailComponentCount, seed);

            UnityEngine.Random.InitState(seed);
            var selectedNodes = SelectFrontNodes(components, DiagramNodeSelectionCount);

            UnityEngine.Random.InitState(seed);
            CreateTrains(environment, components[0], selectedNodes);
            var snapshot = RailGraphNetworkTestHelper.CaptureFromComponents(components);
            return (new ScenarioSetup(environment), snapshot);
        }

        private static List<RailComponent> BuildRailNetwork(
            TrainTestEnvironment environment,
            int railCount, int seed)
        {
            var components = new List<RailComponent>(railCount);

            UnityEngine.Random.InitState(seed);
            var xList = ReturnShuffleList(railCount);
            var yList = ReturnShuffleList(railCount);
            var zList = ReturnShuffleList(railCount);
            var directions = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            var positions = new List<Vector3Int>();
            for (var i = 0; i < railCount; i++)
            {
                var position = new Vector3Int(xList[i], yList[i], zList[i]);
                positions.Add(position);
            }
            for (var i = 0; i < railCount; i++)
            {
                var direction = directions[UnityEngine.Random.Range(0, directions.Length)];
                var component = TrainTestHelper.PlaceRail(environment, positions[i], direction);
                components.Add(component);
            }

            var connected = new HashSet<(int, int, bool, bool)>();

            // Ensure a backbone ring for connectivity.
            for (var i = 0; i < railCount; i++)
            {
                var next = (i + 1) % railCount;
                ConnectComponents(components, connected, i, next, true, true);
            }

            // Add dense deterministic connections similar to ComplexTrainTest's approach.
            for (var i = 0; i < railCount; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    var next = ((i * 10) % railCount + j) % railCount;
                    ConnectComponents(components, connected, i, next, true, true);
                }
            }

            // Add randomized cross connections for complexity.
            var additionalConnections = railCount * 3;
            for (var i = 0; i < additionalConnections; i++)
            {
                var from = UnityEngine.Random.Range(0, railCount);
                var to = UnityEngine.Random.Range(0, railCount);
                if (from == to)
                {
                    continue;
                }

                var useFrontFrom = UnityEngine.Random.Range(0, 2) == 0;
                var useFrontTo = UnityEngine.Random.Range(0, 2) == 0;
                ConnectComponents(components, connected, from, to, useFrontFrom, useFrontTo);
            }

            return components;
        }

        private static List<RailNode> SelectFrontNodes(List<RailComponent> components, int count)
        {
            var frontNodes = components.Select(component => component.FrontNode).ToList();
            var indices = ReturnShuffleList(frontNodes.Count);
            var result = new List<RailNode>(count);
            for (var i = 0; i < count && i < frontNodes.Count; i++)
            {
                result.Add(frontNodes[indices[i]]);
            }
            return result;
        }

        private static List<TrainUnit> CreateTrains(
            TrainTestEnvironment environment,
            RailComponent entryComponent,
            IReadOnlyList<RailNode> diagramNodes)
        {
            var trains = new List<TrainUnit>(TrainCount);
            
            for (var i = 0; i < MasterHolder.TrainUnitMaster.Train.TrainCars.Length; i++)
            {
                var trainCarMaster = MasterHolder.TrainUnitMaster.Train.TrainCars[i];
                var startComponent = TrainTestHelper.PlaceRail(
                    environment,
                    new Vector3Int(-100000 - (i * 17), 0, -50000 + (i * 23)),
                    BlockDirection.South);

                startComponent.ConnectRailComponent(entryComponent, true, true);

                var nodeList = new List<IRailNode>
                {
                    entryComponent.FrontNode,
                    startComponent.FrontNode
                };
                
                var railPosition = new RailPosition(nodeList, trainCarMaster.Length, 0);
                
                var cars = new List<TrainCar>
                {
                    new TrainCar(trainCarMaster)
                };

                var train = new TrainUnit(railPosition, cars);

                var entryCount = UnityEngine.Random.Range(MinDiagramEntries, MaxDiagramEntries + 1);
                for (var j = 0; j < entryCount; j++)
                {
                    var targetNode = diagramNodes[UnityEngine.Random.Range(0, diagramNodes.Count)];
                    var entry = train.trainDiagram.AddEntry(targetNode);
                    entry.SetDepartureWaitTicks(UnityEngine.Random.Range(0, MaxWaitTicks + 1));
                }

                train.TurnOnAutoRun();
                trains.Add(train);
            }

            return trains;
        }

        private static int GenerateUniqueTrainLength(HashSet<int> usedLengths)
        {
            int length;
            do
            {
                length = UnityEngine.Random.Range(1, 10000001);
            } while (!usedLengths.Add(length));

            return length;
        }

        private static void ConnectComponents(
            IReadOnlyList<RailComponent> components,
            HashSet<(int, int, bool, bool)> connected,
            int from,
            int to,
            bool useFrontFrom,
            bool useFrontTo)
        {
            if (from == to)
            {
                return;
            }

            var key = (from, to, useFrontFrom, useFrontTo);
            var reverse = (to, from, !useFrontTo, !useFrontFrom);
            if (connected.Contains(key) || connected.Contains(reverse))
            {
                return;
            }

            components[from].ConnectRailComponent(components[to], useFrontFrom, useFrontTo);
            connected.Add(key);
            connected.Add(reverse);
        }

        private static List<int> ReturnShuffleList(int n)
        {
            var list = new List<int>(n);
            for (var i = 0; i < n; i++)
            {
                list.Add(i);
            }

            for (var i = 0; i < list.Count; i++)
            {
                var j = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }

        private static void AdvanceTicks(int tickCount)
        {
            var action = ManualTickAction;
            for (var i = 0; i < tickCount; i++)
            {
                action();
            }
        }

        private static Action ManualTickAction => _manualTickAction ??= CreateManualTickAction();
        private static Action _manualTickAction;

        private static Action CreateManualTickAction()
        {
            var method = typeof(TrainUpdateService)
                .GetMethod("UpdateTrains1Tickmanually", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "TrainUpdateServiceの手動Tickメソッドが見つかりません。");
            return (Action)Delegate.CreateDelegate(typeof(Action), TrainUpdateService.Instance, method!);
        }
        
        private static void SetupRandomLengthTrainCarMasters(int seed, int count, int tractionForce)
        {
            UnityEngine.Random.InitState(seed);
            var usedLength = new HashSet<int>();
            SetupCustomTrainCarMasters(Enumerable.Range(0, count).Select((_, index) =>
            {
                var length = GenerateUniqueTrainLength(usedLength);
                byte[] trainCarGuidBytes = new byte[16];
                for (int i = 0; i < trainCarGuidBytes.Length; i++)
                {
                    trainCarGuidBytes[i] = (byte)UnityEngine.Random.Range(0, 256);
                }
                byte[] itemGuidBytes = new byte[16];
                for (int i = 0; i < itemGuidBytes.Length; i++)
                {
                    itemGuidBytes[i] = (byte)UnityEngine.Random.Range(0, 256);
                }

                return new TrainCarMasterElement(index, new Guid(trainCarGuidBytes), new Guid(itemGuidBytes), null, tractionForce, 0, length);
            }).ToArray());
        }
        
        private static void SetupCustomTrainCarMasters(TrainCarMasterElement[] elements)
        {
            var trainType = typeof(Train);
            var trainCarsField = trainType.GetField("<TrainCars>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            trainCarsField!.SetValue(MasterHolder.TrainUnitMaster.Train, elements);

            var trainCarMastersByGuid = MasterHolder.TrainUnitMaster.Train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);
            
            var trainUnitMasterType = typeof(TrainUnitMaster);
            var trainCarMastersByGuidField = trainUnitMasterType.GetField("_trainCarMastersByGuid", BindingFlags.NonPublic | BindingFlags.Instance);
            
            trainCarMastersByGuidField!.SetValue(MasterHolder.TrainUnitMaster, trainCarMastersByGuid);
        }

        private static Dictionary<int, TrainSimulationSnapshot> CaptureSnapshots()
        {
            var snapshots = new Dictionary<int, TrainSimulationSnapshot>();
            foreach (var train in TrainUpdateService.Instance.GetRegisteredTrains())
            {
                var snapshot = TrainSimulationSnapshot.Create(train);
                snapshots.Add(snapshot.TrainLength, snapshot);
            }
            return snapshots;
        }

        private static void CleanupTrains()
        {
            var trains = TrainUpdateService.Instance.GetRegisteredTrains().ToList();
            foreach (var train in trains)
            {
                train.OnDestroy();
            }
            TrainUpdateService.Instance.ResetTrains();
        }

        private static void CleanupWorld(TrainTestEnvironment environment)
        {
            var world = environment.WorldBlockDatastore;
            var blocks = world.GetSaveJsonObject();
            foreach (var block in blocks)
            {
                world.RemoveBlock(block.Pos, BlockRemoveReason.ManualRemove);
            }
        }

        private readonly struct ScenarioSetup
        {
            public ScenarioSetup(
                TrainTestEnvironment environment)
            {
                Environment = environment;
            }

            public TrainTestEnvironment Environment { get; }
        }

        private sealed class TrainSimulationSnapshot
        {
            private TrainSimulationSnapshot(
                int trainLength,
                bool isAutoRun,
                int diagramCurrentIndex,
                IReadOnlyList<DiagramEntrySnapshot> diagramEntries,
                int distanceToNextNode,
                double currentSpeed,
                IReadOnlyList<NodeIdentifier> railNodes)
            {
                TrainLength = trainLength;
                IsAutoRun = isAutoRun;
                DiagramCurrentIndex = diagramCurrentIndex;
                DiagramEntries = diagramEntries;
                DistanceToNextNode = distanceToNextNode;
                CurrentSpeed = currentSpeed;
                RailNodes = railNodes;
            }

            public int TrainLength { get; }
            public bool IsAutoRun { get; }
            public int DiagramCurrentIndex { get; }
            public IReadOnlyList<DiagramEntrySnapshot> DiagramEntries { get; }
            public int DistanceToNextNode { get; }
            public double CurrentSpeed { get; }
            public IReadOnlyList<NodeIdentifier> RailNodes { get; }

            public static TrainSimulationSnapshot Create(TrainUnit train)
            {
                var railNodes = train.RailPosition.GetRailNodes()
                    .Select(node => NodeIdentifier.Create(node))
                    .ToList();

                var diagramEntries = train.trainDiagram.Entries
                    .Select(entry => DiagramEntrySnapshot.Create(entry))
                    .ToList();

                return new TrainSimulationSnapshot(
                    train.RailPosition.TrainLength,
                    train.IsAutoRun,
                    train.trainDiagram.CurrentIndex,
                    diagramEntries,
                    train.RailPosition.GetDistanceToNextNode(),
                    train.CurrentSpeed,
                    railNodes);
            }

            public void AssertEqual(TrainSimulationSnapshot other)
            {
                Assert.AreEqual(TrainLength, other.TrainLength, "列車長が一致しません。");
                Assert.AreEqual(IsAutoRun, other.IsAutoRun, "自動運転状態が一致しません。");
                Assert.AreEqual(DiagramEntries.Count, other.DiagramEntries.Count, "ダイアグラムのエントリ数が一致しません。");
                Assert.AreEqual(DiagramCurrentIndex, other.DiagramCurrentIndex, "ダイアグラムの現在インデックスが一致しません。");
                CollectionAssert.AreEqual(RailNodes, other.RailNodes, "RailPositionのノード構成が一致しません。");
                Assert.AreEqual(CurrentSpeed, other.CurrentSpeed, 1e-9, "現在速度が一致しません。");
                Assert.AreEqual(DistanceToNextNode, other.DistanceToNextNode, "次ノードまでの距離が一致しません。");
                for (var i = 0; i < DiagramEntries.Count; i++)
                {
                    DiagramEntries[i].AssertEqual(other.DiagramEntries[i], i);
                }
            }
        }

        private sealed class DiagramEntrySnapshot
        {
            private DiagramEntrySnapshot(
                NodeIdentifier node,
                IReadOnlyList<TrainDiagram.DepartureConditionType> departureConditions,
                int? waitInitial,
                int? waitRemaining)
            {
                Node = node;
                DepartureConditions = departureConditions;
                WaitInitial = waitInitial;
                WaitRemaining = waitRemaining;
            }

            public NodeIdentifier Node { get; }
            public IReadOnlyList<TrainDiagram.DepartureConditionType> DepartureConditions { get; }
            public int? WaitInitial { get; }
            public int? WaitRemaining { get; }

            public static DiagramEntrySnapshot Create(TrainDiagramEntry entry)
            {
                return new DiagramEntrySnapshot(
                    NodeIdentifier.Create(entry.Node),
                    entry.DepartureConditionTypes.ToList(),
                    entry.GetWaitForTicksInitialTicks(),
                    entry.GetWaitForTicksRemainingTicks());
            }

            public void AssertEqual(DiagramEntrySnapshot other, int index)
            {
                Assert.AreEqual(Node, other.Node, $"ダイアグラムエントリ{index}のノードが一致しません。");
                CollectionAssert.AreEqual(DepartureConditions, other.DepartureConditions,
                    $"ダイアグラムエントリ{index}の発車条件が一致しません。");
                Assert.AreEqual(WaitInitial, other.WaitInitial, $"ダイアグラムエントリ{index}の初期待機tickが一致しません。");
                Assert.AreEqual(WaitRemaining, other.WaitRemaining, $"ダイアグラムエントリ{index}の残り待機tickが一致しません。");
            }
        }

        private readonly struct NodeIdentifier : IEquatable<NodeIdentifier>
        {
            private NodeIdentifier(Vector3Int position, int componentIndex, bool isFront)
            {
                Position = position;
                ComponentIndex = componentIndex;
                IsFront = isFront;
            }

            public Vector3Int Position { get; }
            public int ComponentIndex { get; }
            public bool IsFront { get; }

            public static NodeIdentifier Create(IRailNode node)
            {
                var destination = node.ConnectionDestination;
                if (destination.IsDefault())
                {
                    throw new InvalidOperationException("RailNodeからRailComponentIDを取得できませんでした。");
                }

                var componentId = destination.railComponentID;
                return new NodeIdentifier(componentId.Position, componentId.ID, destination.IsFront);
            }

            public bool Equals(NodeIdentifier other)
            {
                return Position == other.Position && ComponentIndex == other.ComponentIndex && IsFront == other.IsFront;
            }

            public override bool Equals(object obj)
            {
                return obj is NodeIdentifier other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Position, ComponentIndex, IsFront);
            }

            public override string ToString()
            {
                return $"{Position}#{ComponentIndex}:{(IsFront ? "F" : "B")}";
            }
        }


    }
}
