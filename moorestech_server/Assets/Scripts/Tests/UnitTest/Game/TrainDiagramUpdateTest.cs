using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramUpdateTest
    {
        [Test]
        public void DiagramRemovesDeletedNodeAndResetsIndex()
        {
            _ = new TrainDiagramManager();

            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            var startNode = RailNode.CreateSingleAndRegister();
            var removedNode = RailNode.CreateSingleAndRegister();
            var nextNode = RailNode.CreateSingleAndRegister();

            startNode.ConnectNode(removedNode, 10);
            removedNode.ConnectNode(startNode, 10);
            removedNode.ConnectNode(nextNode, 15);
            nextNode.ConnectNode(removedNode, 15);
            startNode.ConnectNode(nextNode, 1120);

            var cars = new List<TrainCar>
            {
                TrainTestCarFactory.CreateTrainCar(0, 1000, 0, 0, true)
            };

            var railNodes = new List<IRailNode> { startNode };
            var railPosition = new RailPosition(railNodes, 0, 0);

            var trainUnit = new TrainUnit(railPosition, cars);
            trainUnit.trainDiagram.AddEntry(removedNode);
            trainUnit.trainDiagram.AddEntry(nextNode);

            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun, "自動運転が有効化されている必要があります");
            removedNode.Destroy();
            Assert.IsTrue(trainUnit.IsAutoRun, "自動運転が無効化されず維持されている必要があります。");

            Assert.IsFalse(trainUnit.trainDiagram.Entries.Any(entry => entry.Node == removedNode), "削除したノードがダイアグラムに残っています。");
            Assert.IsTrue(trainUnit.trainDiagram.Entries.Any(entry => entry.Node == nextNode), "残存ノードがダイアグラムから消えてしまっています。");
            Assert.AreEqual(0, trainUnit.trainDiagram.CurrentIndex, "削除後の次エントリ選択インデックスが0にリセットされていません。");
            Assert.AreEqual(nextNode, trainUnit.trainDiagram.GetCurrentNode(), "次の目的地が残存ノードに更新されていません。");

            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.AreEqual(nextNode, trainUnit.trainDiagram.GetCurrentNode(), "単一エントリのダイアグラムで次の目的地が期待ノードに固定されていません。");
        }

        [Test]
        public void DiagramEntrySupportsInventoryEmptyDepartureCondition()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            var trainCar = scenario.TrainCar;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");

            var entry = trainUnit.trainDiagram.Entries.First(e => e.Node == scenario.StationExitFront);
            entry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            Assert.IsFalse(entry.CanDepart(trainUnit), "インベントリが空になるまで列車が待機していません。");

            trainCar.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            Assert.IsTrue(entry.CanDepart(trainUnit), "インベントリが空でも列車が出発可能になっていません。");
        }

        [Test]
        public void DiagramEntryAllowsManagingMultipleDepartureConditions()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            var trainCar = scenario.TrainCar;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            Assert.IsTrue(trainCar.IsDocked, "列車貨車が貨物プラットフォームにドッキングしていません。");

            var entry = trainUnit.trainDiagram.Entries.First(e => e.Node == scenario.StationExitFront);
            entry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            Assert.AreEqual(1, entry.DepartureConditions.Count, "初期の出発条件が1件に設定されていません。");
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.TrainInventoryEmpty },
                entry.DepartureConditionTypes,
                "初期出発条件の種類が想定の列挙値と一致していません。");
            Assert.IsTrue(entry.CanDepart(trainUnit), "インベントリ空で列車が出発可能になっていません。");

            entry.AddDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);

            Assert.AreEqual(2, entry.DepartureConditions.Count, "2つ目の出発条件が追加されていません。");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    TrainDiagram.DepartureConditionType.TrainInventoryEmpty,
                    TrainDiagram.DepartureConditionType.TrainInventoryFull
                },
                entry.DepartureConditionTypes,
                "2種類の出発条件タイプが双方とも登録されていません。");
            Assert.IsFalse(entry.CanDepart(trainUnit), "条件競合時に列車が出発不可になっていません。");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            trainCar.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));

            Assert.IsFalse(entry.CanDepart(trainUnit), "両条件が成立中にもかかわらず列車が出発可能になっています。");

            Assert.IsTrue(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "空条件の削除に失敗しました。");
            Assert.AreEqual(1, entry.DepartureConditions.Count, "条件削除後に残る条件数が1件になっていません。");
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.TrainInventoryFull },
                entry.DepartureConditionTypes,
                "残存する出発条件タイプがTrainInventoryFullではありません。");

            Assert.IsTrue(entry.CanDepart(trainUnit), "満杯条件のみが成立している状態でも列車が出発可能になっていません。");

            Assert.IsFalse(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty),
                "存在しない条件を削除した際の戻り値がfalseではありません。");

            Assert.IsTrue(
                entry.RemoveDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull),
                "最後の条件の削除に失敗しました。");

            Assert.AreEqual(0, entry.DepartureConditions.Count, "全ての条件が削除された後も条件数が0になっていません。");
            Assert.IsTrue(entry.CanDepart(trainUnit), "条件がなくなった状態で列車が即時出発可能になっていません。");

        }

        [Test]
        public void WaitForTicksDepartureConditionRequiresDockedAutoRunAndResetsAfterDeparture()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();

            var trainUnit = scenario.Train;
            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "テスト開始時に列車が貨物プラットフォームへドッキングしていません。");

            var firstEntry = trainUnit.trainDiagram.Entries.First(entry => entry.Node == scenario.StationExitFront);

            firstEntry.SetDepartureWaitTicks(2);
            CollectionAssert.AreEqual(
                new[] { TrainDiagram.DepartureConditionType.WaitForTicks },
                firstEntry.DepartureConditionTypes,
                "待機ティック条件のみが設定されていません。");

            trainUnit.TurnOnAutoRun();
            Assert.IsTrue(trainUnit.IsAutoRun, "待機ティック条件を検証するための自動運転が有効化されていません。");

            trainUnit.trainUnitStationDocking.UndockFromStation();
            Assert.IsFalse(trainUnit.trainUnitStationDocking.IsDocked, "手動のドッキング解除後も接続状態が維持されています。");

            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "ドッキング解除中にも関わらず待機ティックが消費されています。");

            trainUnit.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "カウントダウン再開前に列車が再ドッキングしていません。");

            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "ドッキング再開後の1ティック目で出発可能になっています。");
            Assert.IsTrue(firstEntry.CanDepart(trainUnit), "ドッキング状態で2ティック経過しても待機が完了していません。");

            trainUnit.trainDiagram.MoveToNextEntry();
            trainUnit.trainDiagram.MoveToNextEntry();

            Assert.IsTrue(trainUnit.trainUnitStationDocking.IsDocked, "ダイアグラム遷移後に列車がドッキング状態を維持していません。");
            Assert.IsFalse(firstEntry.CanDepart(trainUnit), "エントリ消化後に待機ティックがリセットされていません。");
        }
    }
}
