using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
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
    public class VanillaBeltConveyorBlockInventoryInserterRoundRobinTest
    {
        /// <summary>
        /// ラウンドロビン選択とInsertItemContext整合性を検証
        /// Verify round-robin selection and InsertItemContext consistency
        /// </summary>
        [Test]
        public void RoundRobinSelectionAndContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // コネクターとインサーターを準備する
            // Prepare connectors and inserter
            var blockPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, blockPosInfo);
            var inserter = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);

            var targets = CreateTargets();
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;
            connectedTargets.Clear();
            foreach (var target in targets) connectedTargets.Add(target.Inventory, target.ConnectedInfo);

            // GetNextGoalConnectorが巡回するか確認する
            // Verify GetNextGoalConnector cycles
            var emptyItemStacks = new List<IItemStack>();
            var first = inserter.GetNextGoalConnector(emptyItemStacks);
            var second = inserter.GetNextGoalConnector(emptyItemStacks);
            var third = inserter.GetNextGoalConnector(emptyItemStacks);
            var fourth = inserter.GetNextGoalConnector(emptyItemStacks);

            Assert.AreNotSame(first, second);
            Assert.AreNotSame(second, third);
            Assert.AreNotSame(first, third);
            Assert.AreSame(first, fourth);

            // InsertItemが均等に分配されるか確認する
            // Verify InsertItem distributes evenly
            inserter.InsertItem(itemStackFactory.Create(new ItemId(1), 1));
            inserter.InsertItem(itemStackFactory.Create(new ItemId(2), 1));
            inserter.InsertItem(itemStackFactory.Create(new ItemId(3), 1));

            foreach (var target in targets)
            {
                Assert.AreEqual(1, target.Inventory.InsertedItems.Count);
            }

            // InsertItemContextの参照が一致するか確認する
            // Verify InsertItemContext references match
            foreach (var target in targets)
            {
                Assert.AreEqual(1, target.Inventory.InsertedContexts.Count);
                var context = target.Inventory.InsertedContexts[0];
                Assert.AreSame(target.ConnectedInfo.SelfConnector, context.SourceConnector);
                Assert.AreSame(target.ConnectedInfo.TargetConnector, context.TargetConnector);
            }
        }

        private static List<RoundRobinTarget> CreateTargets()
        {
            var result = new List<RoundRobinTarget>();
            for (var i = 0; i < 3; i++)
            {
                var selfConnector = CreateInventoryConnector(i * 2, Guid.NewGuid());
                var targetConnector = CreateInventoryConnector(i * 2 + 1, Guid.NewGuid());
                result.Add(new RoundRobinTarget(new DummyBlockInventory(), new ConnectedInfo(selfConnector, targetConnector, null)));
            }
            return result;
        }

        private static BlockConnectInfoElement CreateInventoryConnector(int index, Guid connectorGuid)
        {
            return new BlockConnectInfoElement(index, "Inventory", connectorGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), null);
        }

        private sealed class RoundRobinTarget
        {
            public DummyBlockInventory Inventory { get; }
            public ConnectedInfo ConnectedInfo { get; }

            public RoundRobinTarget(DummyBlockInventory inventory, ConnectedInfo connectedInfo)
            {
                Inventory = inventory;
                ConnectedInfo = connectedInfo;
            }
        }
    }
}
