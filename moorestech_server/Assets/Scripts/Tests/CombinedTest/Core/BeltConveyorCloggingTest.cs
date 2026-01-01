using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class BeltConveyorCloggingTest
    {
        [Test]
        public void InsertRejectedWhenSingleOutputBlockedTest()
        {
            // 依存関係を初期化する
            // Initialize dependencies
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // ベルトと詰まり先を接続する
            // Connect belt and blocked output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            AddTarget(connectedTargets, blockedInventory, 0);

            // 挿入が拒否されることを確認する
            // Verify insertion is rejected
            var item = itemStackFactory.Create(new ItemId(1), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(item));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertAcceptedWhenOneOutputAvailableTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 詰まり先と空き先を接続する
            // Connect blocked and available outputs
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventory = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventory, 1);

            // 挿入と搬出が成功することを確認する
            // Verify insertion and output success
            var item = itemStackFactory.Create(new ItemId(2), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => openInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertRejectedWhenAllOutputsBlockedTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 全接続先が詰まっている状態を作る
            // Set all outputs to blocked
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventoryA = new ConfigurableBlockInventory(1, 1, false, true);
            var blockedInventoryB = new ConfigurableBlockInventory(1, 1, false, true);
            AddTarget(connectedTargets, blockedInventoryA, 0);
            AddTarget(connectedTargets, blockedInventoryB, 1);

            // 挿入が拒否されることを確認する
            // Verify insertion is rejected
            var item = itemStackFactory.Create(new ItemId(3), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(item));
            Assert.AreEqual(0, blockedInventoryA.GetInsertedItemCount());
            Assert.AreEqual(0, blockedInventoryB.GetInsertedItemCount());
        }

        [Test]
        public void OutputRerouteWhenGoalBlockedAtOutputTimeTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 目標先は受け入れ可と判定しつつ搬出で拒否させる
            // Make goal pass InsertionCheck but reject on output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedOnOutputInventory = new ConfigurableBlockInventory(1, 10, true, true);
            var fallbackInventory = new ConfigurableBlockInventory(1, 10, false, false);
            AddTarget(connectedTargets, blockedOnOutputInventory, 0);
            AddTarget(connectedTargets, fallbackInventory, 1);

            // 挿入後に受け入れ先を有効化し、リルートを確認する
            // Enable fallback after insert and verify reroute
            var item = itemStackFactory.Create(new ItemId(4), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            fallbackInventory.SetAllowInsertionCheck(true);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => fallbackInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            Assert.AreEqual(0, blockedOnOutputInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertAllowedAfterBlockedBecomesAvailableTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 最初は挿入不可、後で挿入可能にする
            // Start blocked and allow insertion later
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var dynamicInventory = new ConfigurableBlockInventory(1, 10, false, false);
            AddTarget(connectedTargets, dynamicInventory, 0);

            // 初回は拒否され、二回目で通ることを確認する
            // Verify first insert rejected and second accepted
            var item = itemStackFactory.Create(new ItemId(5), 1);
            var outputRejected = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            dynamicInventory.SetAllowInsertionCheck(true);
            var outputAccepted = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(outputRejected.Equals(item));
            Assert.True(outputAccepted.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => dynamicInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
        }

        [Test]
        public void RoundRobinSkipsBlockedDestinationTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 一つだけ詰まりを混ぜて接続する
            // Connect with one blocked output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventoryA = new ConfigurableBlockInventory(1, 10, true, false);
            var openInventoryB = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventoryA, 1);
            AddTarget(connectedTargets, openInventoryB, 2);

            // 2回挿入して詰まり先が選ばれないことを確認する
            // Insert twice and confirm blocked output is skipped
            var firstOutput = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(6), 1), InsertItemContext.Empty);
            UpdateUntil(() => openInventoryA.GetInsertedItemCount() + openInventoryB.GetInsertedItemCount() >= 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            var secondOutput = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(7), 1), InsertItemContext.Empty);
            UpdateUntil(() => openInventoryA.GetInsertedItemCount() + openInventoryB.GetInsertedItemCount() >= 2, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 2.5));
            Assert.True(firstOutput.Equals(itemStackFactory.CreatEmpty()));
            Assert.True(secondOutput.Equals(itemStackFactory.CreatEmpty()));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
            Assert.AreEqual(1, openInventoryA.GetInsertedItemCount());
            Assert.AreEqual(1, openInventoryB.GetInsertedItemCount());
        }

        [Test]
        public void ContinuousFlowDoesNotStopWithPartialBlockageTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 詰まり先と空き先を接続する
            // Connect blocked and available outputs
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventory = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventory, 1);

            // 連続投入でも停止しないことを確認する
            // Verify continuous flow without stopping
            for (var i = 0; i < 3; i++)
            {
                var output = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(8), 1), InsertItemContext.Empty);
                Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            }
            UpdateUntil(() => openInventory.GetInsertedItemCount() == 3, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 3.5));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void GoalConnectorFallbackWhenDisconnectedTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 2つの接続先を作成してGoalConnectorを保持する
            // Create two outputs and keep GoalConnector
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            beltConveyorComponent.SetTimeOfItemEnterToExit(beltConveyorParam.TimeOfItemEnterToExit * 10);
            var openInventoryA = new ConfigurableBlockInventory(1, 10, true, false);
            var openInventoryB = new ConfigurableBlockInventory(1, 10, true, false);
            var connectorA = AddTarget(connectedTargets, openInventoryA, 0);
            var connectorB = AddTarget(connectedTargets, openInventoryB, 1);
            var output = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(9), 1), InsertItemContext.Empty);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));

            // 接続を外してGoalConnectorが更新されることを確認する
            // Remove connection and verify GoalConnector updates
            connectedTargets.Remove(openInventoryA);
            GameUpdater.UpdateWithWait();
            var beltItem = beltConveyorComponent.BeltConveyorItems[^1];
            Assert.AreEqual(connectorB.ConnectorGuid, beltItem.GoalConnector.ConnectorGuid);
        }

        private static (VanillaBeltConveyorComponent Component, Dictionary<IBlockInventory, ConnectedInfo> ConnectedTargets) CreateBeltConveyor()
        {
            var blockFactory = ServerContext.BlockFactory;
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            return (beltConveyorComponent, connectedTargets);
        }

        private static BlockConnectorInfo AddTarget(Dictionary<IBlockInventory, ConnectedInfo> connectedTargets, IBlockInventory inventory, int index)
        {
            var selfConnector = CreateConnector(index);
            var targetConnector = CreateConnector(index + 100);
            connectedTargets.Add(inventory, new ConnectedInfo(selfConnector, targetConnector, null));
            return selfConnector;
        }

        private static BlockConnectorInfo CreateConnector(int index)
        {
            return new BlockConnectorInfo(Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>(), null);
        }

        private static void UpdateUntil(Func<bool> condition, TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (!condition())
            {
                if (DateTime.Now > endTime) Assert.Fail("Timeout waiting for belt conveyor condition.");
                GameUpdater.UpdateWithWait();
            }
        }
    }
}
