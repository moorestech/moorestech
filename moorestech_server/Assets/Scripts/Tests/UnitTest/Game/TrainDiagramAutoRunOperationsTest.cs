using Core.Master;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramAutoRunOperationsTest
    {
        [SetUp]
        public void SetUp()
        {
            _ = new TrainDiagramManager();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation1_RemovingNonCurrentEntryKeepsAutoRunStable(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var currentDestination = trainUnit.trainDiagram.GetCurrentNode();

            Assert.IsNotNull(currentDestination, "シナリオは有効な現在の目的地から開始している必要があります。");
            Assert.Greater(trainUnit.trainDiagram.Entries.Count, 1,
                "シナリオには削除対象となる“現在以外の”エントリが含まれている必要があります。");

            var dockingStateBefore = trainUnit.trainUnitStationDocking.IsDocked;
            var autoRunBefore = trainUnit.IsAutoRun;

            var nonCurrentNode = trainUnit.trainDiagram.Entries
                .Select(entry => entry.Node)
                .Last(node => node != currentDestination);

            trainUnit.trainDiagram.HandleNodeRemoval(nonCurrentNode);

            Assert.AreEqual(autoRunBefore, trainUnit.IsAutoRun,
                "現在以外のエントリを削除しても自動運転は切り替わらないはずです。");
            Assert.AreEqual(dockingStateBefore, trainUnit.trainUnitStationDocking.IsDocked,
                "現在以外のエントリを削除してもドッキング状態は変化しないはずです。");
            Assert.AreSame(currentDestination, trainUnit.trainDiagram.GetCurrentNode(),
                "現在以外のエントリを削除しても現在の目的地は維持されるはずです。");

            trainUnit.Update();

            Assert.AreEqual(autoRunBefore, trainUnit.IsAutoRun,
                "以降のUpdate後も自動運転は安定して維持されるはずです。");
            Assert.AreEqual(dockingStateBefore, trainUnit.trainUnitStationDocking.IsDocked,
                "以降のUpdate後もドッキング状態は維持されるはずです。");
            Assert.AreSame(currentDestination, trainUnit.trainDiagram.GetCurrentNode(),
                "現在以外のエントリを削除後も、列車は同じ目的地に向かい続けるはずです。");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation2_RemovingCurrentEntryAdvancesAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;

            var diagram = trainUnit.trainDiagram;
            var currentDestination = diagram.GetCurrentNode();

            Assert.IsNotNull(currentDestination, "シナリオは有効な現在の目的地から開始している必要があります。");
            Assert.Greater(diagram.Entries.Count, 1,
                "シナリオには先に進むための次のエントリが含まれている必要があります。");

            var nextIndex = (diagram.CurrentIndex + 1) % diagram.Entries.Count;
            var nextNode = diagram.Entries[nextIndex].Node;

            diagram.HandleNodeRemoval(currentDestination);

            Assert.IsTrue(trainUnit.IsAutoRun, "現在のエントリを削除しても自動運転は有効のままのはずです。");
            Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                "ダイヤグラムの現在エントリは、削除後に次のノードへ進むはずです。");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "停泊シナリオでは、次のティックまで列車はドッキング状態を維持するはずです。");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "現在エントリの削除後、次のティックで列車はドッキング解除されるはずです。");
                Assert.IsTrue(trainUnit.IsAutoRun, "Update後も自動運転は有効のままのはずです。");
                Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                    "ドッキング解除後は次のノードに向かっているはずです。");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "走行中シナリオでは、現在エントリ削除後も非ドッキング状態のはずです。");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "走行中シナリオでは、その後のティックでも非ドッキング状態を維持するはずです。");
                Assert.IsTrue(trainUnit.IsAutoRun, "走行中は自動運転が有効のままのはずです。");
                Assert.AreSame(nextNode, diagram.GetCurrentNode(),
                    "走行中シナリオでは、Update後も進んだ先のノードへ向かい続けるはずです。");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation3_RemovingAllEntriesStopsAutoRun(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            var nodesToRemove = diagram.Entries
                .Select(entry => entry.Node)
                .Distinct()
                .ToList();

            Assert.IsNotEmpty(nodesToRemove,
                "シナリオにはダイヤグラムから削除できるエントリが用意されている必要があります。");

            foreach (var node in nodesToRemove)
            {
                diagram.HandleNodeRemoval(node);
            }

            Assert.IsEmpty(diagram.Entries,
                "すべてのノードを削除した後、ダイヤグラムにエントリは残っていないはずです。");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "停泊シナリオでは、削除直後は列車がドッキング中であると報告されるはずです。");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "すべてのエントリを削除すると、次のティックで列車はドッキング解除されるはずです。");
                Assert.IsFalse(trainUnit.IsAutoRun,
                    "エントリのないダイヤグラムは次の目的地を報告しないはずです。");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "走行中シナリオは非ドッキング状態で開始されるはずです。");

                trainUnit.Update();
            }

            Assert.IsFalse(trainUnit.IsAutoRun,
                "ダイヤグラムの全エントリ削除処理が完了すると自動運転は無効化されるべきです。");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                "自動運転が無効化された後も列車は非ドッキング状態を維持するはずです。");
            Assert.IsNull(diagram.GetCurrentNode(),
                "エントリのないダイヤグラムは次の目的地を報告しないはずです。");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation4_RemovingCurrentEntrySkipsDisconnectedNext(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 3,
                "シナリオには少なくとも3つ（start, n1, n2）のエントリが必要です。");

            var startNode = RequireRailNode(diagram.Entries[0].Node);
            var firstDestination = RequireRailNode(diagram.Entries[1].Node);
            var secondDestination = RequireRailNode(diagram.Entries[2].Node);

            Assert.AreSame(startNode, diagram.GetCurrentNode(),
                "初期の目的地は駅の出口ノードであるべきです。");

            startNode.DisconnectNode(firstDestination);
            firstDestination.DisconnectNode(startNode);

            Assert.IsFalse(startNode.ConnectedNodes.Contains(firstDestination),
                "startノードはもはやfirstDestinationに直接接続していないはずです。");

            const int rerouteDistance = 4321;
            startNode.ConnectNode(secondDestination, rerouteDistance);
            secondDestination.ConnectNode(startNode, rerouteDistance);

            diagram.HandleNodeRemoval(startNode);

            Assert.IsTrue(trainUnit.IsAutoRun,
                "到達経路が残っている場合、現在エントリを削除しても自動運転は有効のままのはずです。");

            if (!startRunning)
            {
                Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked,
                    "停泊シナリオでは、削除直後は依然としてドッキング中のはずです。");

                trainUnit.Update();

                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "現在エントリの削除後、列車はドッキング解除されるはずです。");
                Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                    "停泊シナリオでは、ドッキング解除後に到達可能な2番目の目的地へ進むはずです。");
                Assert.IsTrue(trainUnit.IsAutoRun,
                    "ドッキング解除後も自動運転は有効のままのはずです。");
            }
            else
            {
                Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                    "走行中シナリオは非ドッキング状態で開始されるはずです。");

                const int maxUpdates = 8;
                for (var i = 0; i < maxUpdates; i++)
                {
                    trainUnit.Update();
                    if (ReferenceEquals(diagram.GetCurrentNode(), secondDestination))
                    {
                        break;
                    }
                }

                Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                    "走行中シナリオでは、到達可能な2番目の目的地へ経路再探索されるはずです。");
                Assert.IsTrue(trainUnit.IsAutoRun,
                    "到達可能なノードへルート再探索中も自動運転は有効のはずです。");
            }

            trainUnit.Update();

            Assert.AreSame(secondDestination, diagram.GetCurrentNode(),
                "以降のUpdateでも、列車は到達可能な目的地をターゲットし続けるはずです。");
            Assert.IsTrue(trainUnit.IsAutoRun,
                "経路再探索が安定した後も自動運転は有効のはずです。");
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                "再経路設定後はドッキングではなく走行中であるべきです。");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation5_RemovingCurrentEntryStopsAutoRunWhenNoPathsRemain(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 4,
                "操作5には start, n1, n2, n0 のエントリが必要です。");

            var startNode = RequireRailNode(diagram.Entries[0].Node);
            var firstNode = RequireRailNode(diagram.Entries[1].Node);
            var secondNode = RequireRailNode(diagram.Entries[2].Node);
            var fallbackNode = RequireRailNode(diagram.Entries[3].Node);

            startNode.DisconnectNode(firstNode);
            firstNode.DisconnectNode(startNode);
            firstNode.DisconnectNode(secondNode);
            secondNode.DisconnectNode(firstNode);

            diagram.HandleNodeRemoval(startNode);

            Assert.IsTrue(diagram.Entries.Any(entry => ReferenceEquals(entry.Node, fallbackNode)),
                "startノード削除後もフォールバックノードのエントリは残っているはずです。");

            const int maxUpdates = 48;
            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();
            }

            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked,
                "現在エントリが到達不能になった場合、列車はドッキング解除されるはずです。");
            Assert.IsFalse(trainUnit.IsAutoRun,
                "到達可能なエントリが残っていない場合、最終的に自動運転は無効化されるべきです。");
            Assert.IsNotNull(diagram.GetCurrentNode(),
                "現在のダイヤグラムから次の目的地を取得できるはずです。");

            //ここまで操作５の確認。以下は追加検証。再度nadepathを繋いでdiagramを復旧できるか
            startNode.ConnectNode(firstNode, 9999);
            firstNode.ConnectNode(secondNode, 9999);
            secondNode.ConnectNode(fallbackNode, 9999);
            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun,
                "ダイヤグラム復旧確認");
            Assert.IsNotNull(diagram.GetCurrentNode(),
                "現在のダイヤグラムから次の目的地を取得できるはずです。");

        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation6_InsertingEntryMaintainsCycleOrder(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            Assert.GreaterOrEqual(diagram.Entries.Count, 4,
                "操作6には start, n1, n2, n0 のエントリが必要です。");

            var startNode = RequireRailNode(diagram.Entries[0].Node);
            var n1 = RequireRailNode(diagram.Entries[1].Node);
            var n2 = RequireRailNode(diagram.Entries[2].Node);
            var n0 = RequireRailNode(diagram.Entries[3].Node);

            ConnectNodePair(n2, n0, 7777);

            Assert.AreEqual(4, diagram.Entries.Count,
                "n2 を削除した後、ダイヤグラムには start, n1, n0 が含まれているはずです。");
            Assert.AreSame(startNode, diagram.Entries[0].Node,
                "挿入前の現在エントリは start ノードのままであるべきです。");
            Assert.AreSame(n1, diagram.Entries[1].Node,
                "挿入前の2番目のエントリは n1 のはずです。");
            Assert.AreSame(n2, diagram.Entries[2].Node,
                "挿入前の3番目のエントリは n0 のはずです。");

            CollectionAssert.AreEqual(
                new[] { startNode, n1, n2, n0 },
                diagram.Entries.Select(entry => entry.Node).ToArray(),
                "挿入後は start → n1 → n2 → n0 の順になるはずです。");

            var startEntry = diagram.Entries[0];
            startEntry.SetDepartureWaitTicks(0);

            var visitedDestinations = new List<RailNode> { RequireRailNode(diagram.GetCurrentNode()) };
            Assert.AreSame(startNode, visitedDestinations[0], "開始ノードは startNode でなければなりません。");
            const int maxUpdates = 65536;

            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();
                var currentDestination = RequireRailNode(diagram.GetCurrentNode());
                if (!ReferenceEquals(currentDestination, visitedDestinations.Last()))
                {
                    visitedDestinations.Add(currentDestination);
                    if (ReferenceEquals(currentDestination, startNode) && visitedDestinations.Count > 1)
                    {
                        break;
                    }
                }
            }

            Assert.AreEqual(5, visitedDestinations.Count, "visitedDestinations は 5 つのノードを含むべきです。");

            CollectionAssert.AreEqual(
                new[] { startNode, n1, n2, n0, startNode },
                visitedDestinations,
                "挿入後、ダイヤグラムは start → n1 → n2 → n0 → start と巡回するはずです。");
            Assert.IsTrue(trainUnit.IsAutoRun,
                "この巡回の間、自動運転は常に有効であるべきです。");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Operation7_SingleEntryLoopsDockAndDepart(bool startRunning)
        {
            using var scenario = CreateScenario(startRunning);
            var trainUnit = scenario.Train;
            var diagram = trainUnit.trainDiagram;

            foreach (var entry in diagram.Entries.ToList())
            {
                if (!ReferenceEquals(entry.Node, diagram.Entries[0].Node))
                {
                    diagram.HandleNodeRemoval(entry.Node);
                }
            }

            Assert.AreEqual(1, diagram.Entries.Count,
                "単一エントリ・ループのテストでは、ダイヤグラムはちょうど1つのエントリのみを含むべきです。");

            var startEntry = diagram.Entries[0];
            var trainCar = scenario.TrainCar;

            if (startRunning)
            {
                startEntry.SetDepartureWaitTicks(0);
                for (var i = 0; i < 9999; i++)
                {
                    trainUnit.Update();
                    if (trainUnit.trainUnitStationDocking.IsDocked) break;
                }
            }
            else
            {
                startEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);
                var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
                trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));
            }

            var previousDockState = trainUnit.trainUnitStationDocking.IsDocked;
            var docked = previousDockState;

            const int maxUpdates = 256;
            for (var i = 0; i < maxUpdates; i++)
            {
                trainUnit.Update();
                previousDockState = docked;
                docked = trainUnit.trainUnitStationDocking.IsDocked;
                if (i == 0) continue;
                if (startRunning == false)
                    Assert.IsTrue(docked, "単一エントリ・ループでは、各Updateでドッキング状態が切り替わるべきです。");
                if (startRunning == true)
                    Assert.IsTrue(docked, "単一エントリ・ループでは、各Updateでドッキング状態が切り替わるべきです。");
            }
            Assert.AreSame(diagram.Entries[0].Node, diagram.GetCurrentNode(), "単一エントリのダイヤグラムでは、常にその単一ノードを目標にし続けるはずです。");
        }

        private static TrainAutoRunTestScenario CreateScenario(bool startRunning)
        {
            return startRunning
                ? TrainAutoRunTestScenario.CreateRunningScenario()
                : TrainAutoRunTestScenario.CreateDockedScenario();
        }

        // RailNode型を強制的に取得する
        // Forcefully obtain a concrete RailNode instance
        private static RailNode RequireRailNode(IRailNode node)
        {
            Assert.IsNotNull(node, "IRailNodeがnullです / Target IRailNode is null.");
            Assert.IsInstanceOf<RailNode>(node, "IRailNodeがRailNodeではありません / IRailNode is not a RailNode.");
            return (RailNode)node;
        }

        private static void ConnectNodePair(RailNode first, RailNode second, int distance)
        {
            if (first == null || second == null)
            {
                return;
            }

            if (distance <= 0)
            {
                distance = 1;
            }

            first.ConnectNode(second, distance);
            second.ConnectNode(first, distance);
        }
    }
}
