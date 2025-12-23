using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Extension;
using Game.Context;

namespace Tests.UnitTest.Game
{
    public class TrainSingleTwoStationIntegrationTest
    {
        [Test]
        public void TrainCompletesRoundTripBetweenTwoCargoPlatforms()
        {
            _ = new TrainDiagramManager();

            var env = TrainTestHelper.CreateEnvironment();

            var (loadingBlock, loadingSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);

            var (unloadingBlock, unloadingSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(0, 0, 10),
                BlockDirection.North);

            Assert.IsNotNull(loadingBlock, "積込プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(loadingSaver, "積込プラットフォームのRailSaverComponentが取得できませんでした。");
            Assert.IsNotNull(unloadingBlock, "荷降ろしプラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(unloadingSaver, "荷降ろしプラットフォームのRailSaverComponentが取得できませんでした。");

            var loadingEntryComponent = loadingSaver.RailComponents[0];
            var loadingExitComponent = loadingSaver.RailComponents[1];
            var unloadingEntryComponent = unloadingSaver.RailComponents[0];
            var unloadingExitComponent = unloadingSaver.RailComponents[1];

            var transitRailA = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 3), BlockDirection.North);
            var transitRailB = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 6), BlockDirection.North);

            const int TransitSegmentLength = 2000;
            ConnectFront(loadingExitComponent, transitRailA, TransitSegmentLength);
            ConnectFront(transitRailA, transitRailB, TransitSegmentLength);
            ConnectFront(transitRailB, unloadingEntryComponent, TransitSegmentLength);
            ConnectFront(unloadingExitComponent, loadingEntryComponent, TransitSegmentLength);

            Assert.IsTrue(loadingBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var loadingInventory),
                "積込プラットフォームのインベントリコンポーネントが見つかりません。");
            Assert.IsTrue(unloadingBlock.ComponentManager.TryGetComponent<IBlockInventory>(out var unloadingInventory),
                "荷降ろしプラットフォームのインベントリコンポーネントが見つかりません。");

            var cargoPlatformLoader = loadingBlock.GetComponent<CargoplatformComponent>();
            var cargoPlatformUnloader = unloadingBlock.GetComponent<CargoplatformComponent>();
            Assert.IsNotNull(cargoPlatformLoader, "積込プラットフォームのコンポーネント取得に失敗しました。");
            Assert.IsNotNull(cargoPlatformUnloader, "荷降ろしプラットフォームのコンポーネント取得に失敗しました。");

            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1);
            var maxStack = itemMaster.MaxStack;
            loadingInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));
            unloadingInventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            cargoPlatformLoader.SetTransferMode(CargoplatformComponent.CargoTransferMode.LoadToTrain);
            cargoPlatformUnloader.SetTransferMode(CargoplatformComponent.CargoTransferMode.UnloadToPlatform);

            var stationSegmentLength = loadingEntryComponent.FrontNode.GetDistanceToNode(loadingExitComponent.FrontNode);
            Assert.Greater(stationSegmentLength, 0, "プラットフォーム間セグメントの長さが0以下になっています。");

            var initialRailNodes = new List<RailNode>
            {
                loadingExitComponent.FrontNode,
                loadingEntryComponent.FrontNode
            };


            var railPosition = new RailPosition(new List<IRailNode>(initialRailNodes), stationSegmentLength, 0);
            var trainCar = new TrainCar(new TrainCarMasterElement(0, Guid.Empty, Guid.Empty, null, 1000, 1, stationSegmentLength));
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar });

            var loadingEntry = trainUnit.trainDiagram.AddEntry(loadingExitComponent.FrontNode);
            loadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);

            var unloadingEntry = trainUnit.trainDiagram.AddEntry(unloadingExitComponent.FrontNode);
            unloadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            trainUnit.TurnOnAutoRun();
            trainUnit.Update();

            Assert.IsTrue(trainCar.IsDocked, "列車が積込プラットフォームにドッキングした状態で開始していません。");
            Assert.AreSame(loadingBlock, trainCar.dockingblock, "列車が最初に積込プラットフォームへドッキングしていません。");

            AdvanceUntil(trainUnit, () => trainCar.IsInventoryFull(), maxIterations: maxStack * 4,
                "積込プラットフォームにドッキング中に列車インベントリが満杯になりませんでした");

            var depletedStack = loadingInventory.GetItem(0);
            Assert.AreEqual(ItemMaster.EmptyItemId, depletedStack.Id, "積込プラットフォームが列車へ全量を移送できていません。");

            AdvanceUntil(trainUnit, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "積込完了後に列車が出発しませんでした");

            AdvanceUntil(trainUnit,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, unloadingBlock),
                maxIterations: 25000,
                "列車が荷降ろしプラットフォームに到達しませんでした");

            AdvanceUntil(trainUnit, () => trainCar.IsInventoryEmpty(), maxIterations: maxStack * 4,
                "荷降ろしプラットフォームにドッキング中に列車インベントリが空になりませんでした");

            var receivedStack = unloadingInventory.GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedStack.Id, "荷降ろしプラットフォームが輸送アイテムを受け取っていません。");
            Assert.AreEqual(maxStack, receivedStack.Count,
                "荷降ろしプラットフォームが列車から全量を受け取っていません。");

            AdvanceUntil(trainUnit, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "荷降ろし後に列車が出発しませんでした");

            AdvanceUntil(trainUnit,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, loadingBlock),
                maxIterations: 25000,
                "列車がループ完了のために積込プラットフォームへ戻っていません");
        }

        #region Helpers

        private static void ConnectFront(RailComponent source, RailComponent target, int explicitDistance)
        {
            source.ConnectRailComponent(target, true, true, explicitDistance);
        }

        private static void AdvanceUntil(TrainUnit trainUnit, Func<bool> predicate, int maxIterations, string failureMessage)
        {
            for (var i = 0; i < maxIterations; i++)
            {
                trainUnit.Update();
                if (predicate())
                {
                    return;
                }
            }

            Assert.Fail(failureMessage);
        }

        #endregion
    }
}
