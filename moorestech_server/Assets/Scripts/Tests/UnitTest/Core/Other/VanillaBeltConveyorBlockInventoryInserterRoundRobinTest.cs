using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    /// VanillaBeltConveyorBlockInventoryInserterのラウンドロビンロジックをテスト
    /// Test round robin logic of VanillaBeltConveyorBlockInventoryInserter
    /// </summary>
    public class VanillaBeltConveyorBlockInventoryInserterRoundRobinTest
    {
        /// <summary>
        /// GetFirstGoalConnectorがラウンドロビンでConnectorを選択することをテスト
        /// Test that GetFirstGoalConnector selects connectors with round robin
        /// </summary>
        [Test]
        public void GetFirstGoalConnectorRoundRobinTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // テスト用のBlockConnectorComponentとDummyBlockInventoryを作成
            // Create test BlockConnectorComponent and DummyBlockInventory
            var beltPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;

            // 3つのDummyBlockInventoryを接続先として追加
            // Add 3 DummyBlockInventory as connected targets
            var dummy1 = new DummyBlockInventory();
            var dummy2 = new DummyBlockInventory();
            var dummy3 = new DummyBlockInventory();

            var connector1 = CreateInventoryConnector(0, "path-1");
            var connector2 = CreateInventoryConnector(1, "path-2");
            var connector3 = CreateInventoryConnector(2, "path-3");

            var targetConnector1 = CreateInventoryConnector(10, "target-1");
            var targetConnector2 = CreateInventoryConnector(11, "target-2");
            var targetConnector3 = CreateInventoryConnector(12, "target-3");

            connectedTargets.Add(dummy1, new ConnectedInfo(connector1, targetConnector1, null));
            connectedTargets.Add(dummy2, new ConnectedInfo(connector2, targetConnector2, null));
            connectedTargets.Add(dummy3, new ConnectedInfo(connector3, targetConnector3, null));

            // Inserterを作成
            // Create Inserter
            var inserter = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);

            // 3回呼び出して全て異なるConnectorが返ることを検証
            // Call 3 times and verify all different connectors are returned
            var returnedConnectors = new HashSet<Guid>();
            for (var i = 0; i < 3; i++)
            {
                var connector = inserter.GetFirstGoalConnector();
                Assert.IsNotNull(connector);
                returnedConnectors.Add(connector.ConnectorGuid);
            }
            Assert.AreEqual(3, returnedConnectors.Count, "3回の呼び出しで3種類のConnectorが返されるべき / 3 calls should return 3 different connectors");

            // 4回目は1回目と同じConnectorになることを検証（ラウンドロビン）
            // 4th call should return the same connector as the 1st call (round robin)
            var fourthConnector = inserter.GetFirstGoalConnector();
            Assert.IsNotNull(fourthConnector);
            Assert.IsTrue(returnedConnectors.Contains(fourthConnector.ConnectorGuid), "4回目のConnectorは既出のConnectorと一致すべき / 4th connector should match one of the previous connectors");
        }

        /// <summary>
        /// InsertItemがラウンドロビンでアイテムを分配することをテスト
        /// Test that InsertItem distributes items with round robin
        /// </summary>
        [Test]
        public void InsertItemRoundRobinDistributionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // テスト用のBlockConnectorComponentとDummyBlockInventoryを作成
            // Create test BlockConnectorComponent and DummyBlockInventory
            var beltPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;

            // 3つのDummyBlockInventoryを接続先として追加
            // Add 3 DummyBlockInventory as connected targets
            var dummy1 = new DummyBlockInventory();
            var dummy2 = new DummyBlockInventory();
            var dummy3 = new DummyBlockInventory();

            var connector1 = CreateInventoryConnector(0, "path-1");
            var connector2 = CreateInventoryConnector(1, "path-2");
            var connector3 = CreateInventoryConnector(2, "path-3");

            var targetConnector1 = CreateInventoryConnector(10, "target-1");
            var targetConnector2 = CreateInventoryConnector(11, "target-2");
            var targetConnector3 = CreateInventoryConnector(12, "target-3");

            connectedTargets.Add(dummy1, new ConnectedInfo(connector1, targetConnector1, null));
            connectedTargets.Add(dummy2, new ConnectedInfo(connector2, targetConnector2, null));
            connectedTargets.Add(dummy3, new ConnectedInfo(connector3, targetConnector3, null));

            // Inserterを作成
            // Create Inserter
            var inserter = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);

            // 3回InsertItemを呼び出す
            // Call InsertItem 3 times
            var item1 = itemStackFactory.Create(new ItemId(1), 1);
            var item2 = itemStackFactory.Create(new ItemId(2), 1);
            var item3 = itemStackFactory.Create(new ItemId(3), 1);

            inserter.InsertItem(item1);
            inserter.InsertItem(item2);
            inserter.InsertItem(item3);

            // 各DummyBlockInventoryに1つずつアイテムが入っていることを検証
            // Verify each DummyBlockInventory has 1 item
            Assert.AreEqual(1, dummy1.InsertedItems.Count, "dummy1に1つのアイテムがあるべき / dummy1 should have 1 item");
            Assert.AreEqual(1, dummy2.InsertedItems.Count, "dummy2に1つのアイテムがあるべき / dummy2 should have 1 item");
            Assert.AreEqual(1, dummy3.InsertedItems.Count, "dummy3に1つのアイテムがあるべき / dummy3 should have 1 item");
        }

        /// <summary>
        /// InsertItemContext内のSourceConnector/TargetConnectorがConnectedInfoと一致することをテスト
        /// Test that SourceConnector/TargetConnector in InsertItemContext matches ConnectedInfo
        /// </summary>
        [Test]
        public void InsertItemContextConnectorMatchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // テスト用のBlockConnectorComponentとDummyBlockInventoryを作成
            // Create test BlockConnectorComponent and DummyBlockInventory
            var beltPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;

            // 1つのDummyBlockInventoryを接続先として追加
            // Add 1 DummyBlockInventory as connected target
            var dummy = new DummyBlockInventory();

            var selfConnector = CreateInventoryConnector(0, "self-path");
            var targetConnector = CreateInventoryConnector(10, "target-path");

            connectedTargets.Add(dummy, new ConnectedInfo(selfConnector, targetConnector, null));

            // Inserterを作成
            // Create Inserter
            var blockInstanceId = new BlockInstanceId(999);
            var inserter = new VanillaBeltConveyorBlockInventoryInserter(blockInstanceId, blockConnector);

            // InsertItemを呼び出す
            // Call InsertItem
            var item = itemStackFactory.Create(new ItemId(1), 1);
            inserter.InsertItem(item);

            // InsertItemContextが正しく設定されていることを検証
            // Verify InsertItemContext is correctly set
            Assert.AreEqual(1, dummy.InsertedContexts.Count);
            var context = dummy.InsertedContexts[0];

            // SourceBlockInstanceIdが一致すること
            // SourceBlockInstanceId should match
            Assert.AreEqual(blockInstanceId, context.SourceBlockInstanceId);

            // SourceConnectorがConnectedInfo.SelfConnectorと同一参照であること
            // SourceConnector should be the same reference as ConnectedInfo.SelfConnector
            Assert.AreSame(selfConnector, context.SourceConnector, "SourceConnectorはConnectedInfo.SelfConnectorと同一参照であるべき / SourceConnector should be same reference as ConnectedInfo.SelfConnector");

            // TargetConnectorがConnectedInfo.TargetConnectorと同一参照であること
            // TargetConnector should be the same reference as ConnectedInfo.TargetConnector
            Assert.AreSame(targetConnector, context.TargetConnector, "TargetConnectorはConnectedInfo.TargetConnectorと同一参照であるべき / TargetConnector should be same reference as ConnectedInfo.TargetConnector");

            // ConnectorGuidも一致すること
            // ConnectorGuid should also match
            Assert.AreEqual(selfConnector.ConnectorGuid, context.SourceConnector.ConnectorGuid);
            Assert.AreEqual(targetConnector.ConnectorGuid, context.TargetConnector.ConnectorGuid);
        }

        /// <summary>
        /// ConnectedCountが正しく接続数を返すことをテスト
        /// Test that ConnectedCount returns correct number of connections
        /// </summary>
        [Test]
        public void ConnectedCountTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // テスト用のBlockConnectorComponentとDummyBlockInventoryを作成
            // Create test BlockConnectorComponent and DummyBlockInventory
            var beltPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;

            // Inserterを作成
            // Create Inserter
            var inserter = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);

            // 初期状態で0
            // Initially 0
            Assert.AreEqual(0, inserter.ConnectedCount);

            // 1つ追加
            // Add 1
            var dummy1 = new DummyBlockInventory();
            var connector1 = CreateInventoryConnector(0, "path-1");
            var targetConnector1 = CreateInventoryConnector(10, "target-1");
            connectedTargets.Add(dummy1, new ConnectedInfo(connector1, targetConnector1, null));
            Assert.AreEqual(1, inserter.ConnectedCount);

            // もう2つ追加
            // Add 2 more
            var dummy2 = new DummyBlockInventory();
            var dummy3 = new DummyBlockInventory();
            var connector2 = CreateInventoryConnector(1, "path-2");
            var connector3 = CreateInventoryConnector(2, "path-3");
            var targetConnector2 = CreateInventoryConnector(11, "target-2");
            var targetConnector3 = CreateInventoryConnector(12, "target-3");
            connectedTargets.Add(dummy2, new ConnectedInfo(connector2, targetConnector2, null));
            connectedTargets.Add(dummy3, new ConnectedInfo(connector3, targetConnector3, null));
            Assert.AreEqual(3, inserter.ConnectedCount);
        }

        private static BlockConnectInfoElement CreateInventoryConnector(int index, string pathId)
        {
            return new BlockConnectInfoElement(index, "Inventory", Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>(), new InventoryConnectOption(pathId));
        }
    }
}
