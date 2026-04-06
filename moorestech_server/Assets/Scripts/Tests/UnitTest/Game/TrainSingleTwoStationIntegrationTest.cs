using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.Unit.Containers;

namespace Tests.UnitTest.Game
{
    public class TrainSingleTwoStationIntegrationTest
    {
        [Test]
        public void TrainCompletesRoundTripBetweenTwoCargoPlatforms()
        {
            _ = new TrainDiagramManager();

            var env = TrainTestHelper.CreateEnvironment();

            var (loadingBlock, loadingComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);

            var (unloadingBlock, unloadingComponents) = TrainTestHelper.PlaceBlockWithRailComponents(
                env,
                ForUnitTestModBlockId.TestTrainItemPlatform,
                new Vector3Int(0, 0, 10),
                BlockDirection.North);

            Assert.IsNotNull(loadingBlock, "積込プラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(loadingComponents, "積込プラットフォームのRailComponentが取得できませんでした。");
            Assert.IsNotNull(unloadingBlock, "荷降ろしプラットフォームブロックの設置に失敗しました。");
            Assert.IsNotNull(unloadingComponents, "荷降ろしプラットフォームのRailComponentが取得できませんでした。");

            var loadingEntryComponent = loadingComponents[0];
            var loadingExitComponent = loadingComponents[1];
            var unloadingEntryComponent = unloadingComponents[0];
            var unloadingExitComponent = unloadingComponents[1];

            var transitRailA = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 3), BlockDirection.North);
            var transitRailB = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 6), BlockDirection.North);

            const int transitSegmentLength = 2000;
            ConnectFront(loadingExitComponent, transitRailA, transitSegmentLength);
            ConnectFront(transitRailA, transitRailB, transitSegmentLength);
            ConnectFront(transitRailB, unloadingEntryComponent, transitSegmentLength);
            ConnectFront(unloadingExitComponent, loadingEntryComponent, transitSegmentLength);

            Assert.IsTrue(loadingBlock.ComponentManager.TryGetComponent<TrainPlatformItemContainerComponent>(out var loadingContainer),
                "積込プラットフォームのインベントリコンポーネントが見つかりません。");
            Assert.IsTrue(unloadingBlock.ComponentManager.TryGetComponent<TrainPlatformItemContainerComponent>(out var unloadingContainer),
                "荷降ろしプラットフォームのインベントリコンポーネントが見つかりません。");
            
            Assert.IsNull(loadingContainer.Container);
            Assert.IsNull(unloadingContainer.Container);
            
            var loaderTrainPlatformTransfer = loadingBlock.GetComponent<TrainPlatformTransferComponent>();
            var unloaderTrainPlatformTransfer = unloadingBlock.GetComponent<TrainPlatformTransferComponent>();
            Assert.IsNotNull(loaderTrainPlatformTransfer, "積込プラットフォームのコンポーネント取得に失敗しました。");
            Assert.IsNotNull(unloaderTrainPlatformTransfer, "荷降ろしプラットフォームのコンポーネント取得に失敗しました。");

            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1);
            var maxStack = itemMaster.MaxStack;
            loadingContainer.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack));
            unloadingContainer.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            loaderTrainPlatformTransfer.SetMode(TrainPlatformTransferComponent.TransferMode.LoadToTrain);
            unloaderTrainPlatformTransfer.SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            Action tickCargoArms = () =>
            {
                foreach (var updatableBlockComponent in loadingBlock.GetComponents<IUpdatableBlockComponent>()) updatableBlockComponent.Update();
                foreach (var updatableBlockComponent in unloadingBlock.GetComponents<IUpdatableBlockComponent>()) updatableBlockComponent.Update();
            };

            var stationSegmentLength = loadingBlock!.BlockPositionInfo.BlockSize.z;
            Assert.Greater(stationSegmentLength, 0, "プラットフォーム間セグメントの長さが0以下になっています。");

            var initialRailNodes = new List<RailNode>
            {
                loadingExitComponent.FrontNode,
                loadingEntryComponent.FrontNode
            };


            var railPosition = new RailPosition(new List<IRailNode>(initialRailNodes), stationSegmentLength, 0);
            var trainCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, stationSegmentLength, true).trainCar;
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, env.GetTrainUpdateService(), env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());

            var loadingEntry = trainUnit.trainDiagram.AddEntry(loadingExitComponent.FrontNode);
            loadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryFull);

            var unloadingEntry = trainUnit.trainDiagram.AddEntry(unloadingExitComponent.FrontNode);
            unloadingEntry.SetDepartureCondition(TrainDiagram.DepartureConditionType.TrainInventoryEmpty);

            trainUnit.TurnOnAutoRun();
            trainUnit.Update();

            Assert.IsTrue(trainCar.IsDocked, "列車が積込プラットフォームにドッキングした状態で開始していません。");
            Assert.AreSame(loadingBlock, trainCar.dockingblock, "列車が最初に積込プラットフォームへドッキングしていません。");

            AdvanceUntil(trainUnit, tickCargoArms, () => trainCar.IsInventoryFull(), maxIterations: maxStack * 4,
                "積込プラットフォームにドッキング中に列車インベントリが満杯になりませんでした");

            var depletedStack = loadingContainer.Container!.InventoryItems[0];
            Assert.AreEqual(ItemMaster.EmptyItemId, depletedStack.Stack.Id, "積込プラットフォームが列車へ全量を移送できていません。");

            AdvanceUntil(trainUnit, tickCargoArms, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "積込完了後に列車が出発しませんでした");

            AdvanceUntil(trainUnit, tickCargoArms,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, unloadingBlock),
                maxIterations: 25000,
                "列車が荷降ろしプラットフォームに到達しませんでした");

            AdvanceUntil(trainUnit, tickCargoArms, () => trainCar.IsInventoryEmpty(), maxIterations: maxStack * 4,
                "荷降ろしプラットフォームにドッキング中に列車インベントリが空になりませんでした");

            var receivedStack = unloadingContainer.Container!.InventoryItems[0];
            Assert.AreEqual(ForUnitTestItemId.ItemId1, receivedStack.Stack.Id, "荷降ろしプラットフォームが輸送アイテムを受け取っていません。");
            Assert.AreEqual(maxStack, receivedStack.Stack.Count,
                "荷降ろしプラットフォームが列車から全量を受け取っていません。");

            AdvanceUntil(trainUnit, tickCargoArms, () => !trainUnit.trainUnitStationDocking.IsDocked, maxIterations: 120,
                "荷降ろし後に列車が出発しませんでした");

            AdvanceUntil(trainUnit, tickCargoArms,
                () => trainCar.IsDocked && ReferenceEquals(trainCar.dockingblock, loadingBlock),
                maxIterations: 25000,
                "列車がループ完了のために積込プラットフォームへ戻っていません");
        }

        #region Helpers

        private static void ConnectFront(RailComponent source, RailComponent target, int explicitDistance)
        {
            //source.ConnectRailComponent(target, true, true, explicitDistance);
            source.FrontNode.ConnectNode(target.FrontNode, explicitDistance);
            target.BackNode.ConnectNode(source.BackNode, explicitDistance);
        }

        private static void AdvanceUntil(TrainUnit trainUnit, Action tickAction, Func<bool> predicate, int maxIterations, string failureMessage)
        {
            for (var i = 0; i < maxIterations; i++)
            {
                tickAction();
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
