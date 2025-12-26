using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BeltConveyorSaveLoadTest
    {
        [Test]
        public void NormalSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(1), beltPosInfo);
            
            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);
            
            //アイテムを設定
            var item0GoalGuid = Guid.NewGuid();
            var item2GoalGuid = Guid.NewGuid();
            var item3GoalGuid = Guid.NewGuid();
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), CreateInventoryConnector(0, "input-0", Guid.NewGuid()), CreateInventoryConnector(1, "goal-0", item0GoalGuid))
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), CreateInventoryConnector(2, "input-2", Guid.NewGuid()), CreateInventoryConnector(3, "goal-2", item2GoalGuid))
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), CreateInventoryConnector(4, "input-3", Guid.NewGuid()), CreateInventoryConnector(5, "goal-3", item3GoalGuid))
            {
                RemainingPercent = 0.9f,
            };


            //セーブデータ取得
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };
            Debug.Log(str);


            //セーブデータをロード
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);
            
            // Guid一致の接続先を用意
            // Prepare targets matching Guid
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(10, "goal-0", item0GoalGuid), CreateInventoryConnector(11, "target-0", Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(12, "goal-2", item2GoalGuid), CreateInventoryConnector(13, "target-2", Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(14, "goal-3", item3GoalGuid), CreateInventoryConnector(15, "target-3", Guid.NewGuid()), null));

            var newBelt = new VanillaBeltConveyorComponent(states, 4, 4000, beltConveyorConnector, BeltConveyorSlopeType.Straight);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);

            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.8f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.85f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(0.9f, newInventoryItems[3].RemainingPercent);

            // Guidが復元され、接続先が解決されるかチェック
            // Verify Guid is restored and target is resolved
            newBelt.Update();
            Assert.AreEqual(item0GoalGuid, newInventoryItems[0].GetGoalConnectorGuid());
            Assert.AreEqual(item2GoalGuid, newInventoryItems[2].GetGoalConnectorGuid());
            Assert.AreEqual(item3GoalGuid, newInventoryItems[3].GetGoalConnectorGuid());
            Assert.AreEqual(item0GoalGuid, newInventoryItems[0].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item2GoalGuid, newInventoryItems[2].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item3GoalGuid, newInventoryItems[3].GoalConnector.ConnectorGuid);
        }

        [Test]
        public void GearSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.GearBeltConveyor, new BlockInstanceId(1), beltPosInfo);

            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            //アイテムを設定
            var item0GoalGuid = Guid.NewGuid();
            var item2GoalGuid = Guid.NewGuid();
            var item3GoalGuid = Guid.NewGuid();
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), CreateInventoryConnector(0, "input-0", Guid.NewGuid()), CreateInventoryConnector(1, "goal-0", item0GoalGuid))
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), CreateInventoryConnector(2, "input-2", Guid.NewGuid()), CreateInventoryConnector(3, "goal-2", item2GoalGuid))
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), CreateInventoryConnector(4, "input-3", Guid.NewGuid()), CreateInventoryConnector(5, "goal-3", item3GoalGuid))
            {
                RemainingPercent = 0.9f,
            };
            
            
            //セーブデータ取得
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };
            Debug.Log(str);
            
            
            //セーブデータをロード
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);
            
            // Guid一致の接続先を用意
            // Prepare targets matching Guid
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)blockConnector.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(10, "goal-0", item0GoalGuid), CreateInventoryConnector(11, "target-0", Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(12, "goal-2", item2GoalGuid), CreateInventoryConnector(13, "target-2", Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(14, "goal-3", item3GoalGuid), CreateInventoryConnector(15, "target-3", Guid.NewGuid()), null));

            var newBelt = new VanillaBeltConveyorComponent(states, 4, 4000, beltConveyorConnector, BeltConveyorSlopeType.Straight);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);

            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.8f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.85f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(0.9f, newInventoryItems[3].RemainingPercent);

            // Guidが復元され、接続先が解決されるかチェック
            // Verify Guid is restored and target is resolved
            newBelt.Update();
            Assert.AreEqual(item0GoalGuid, newInventoryItems[0].GetGoalConnectorGuid());
            Assert.AreEqual(item2GoalGuid, newInventoryItems[2].GetGoalConnectorGuid());
            Assert.AreEqual(item3GoalGuid, newInventoryItems[3].GetGoalConnectorGuid());
            Assert.AreEqual(item0GoalGuid, newInventoryItems[0].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item2GoalGuid, newInventoryItems[2].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item3GoalGuid, newInventoryItems[3].GoalConnector.ConnectorGuid);
        }

        private static BlockConnectInfoElement CreateInventoryConnector(int index, string pathId, Guid connectorGuid)
        {
            return new BlockConnectInfoElement(index, "Inventory", connectorGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), new InventoryConnectOption(pathId));
        }

        /// <summary>
        /// セーブ/ロード後にコネクター情報がnullになることをテスト（現状の仕様）
        /// Test that connector information becomes null after save/load (current specification)
        /// </summary>
        /// <remarks>
        /// 現状の実装では、セーブデータにコネクター情報を含めていないため、
        /// ロード後のStartConnector/GoalConnectorはnullになる。
        /// 将来的にコネクター情報の永続化が必要になった場合、このテストを更新すること。
        ///
        /// Currently, the save data does not include connector information,
        /// so StartConnector/GoalConnector become null after loading.
        /// Update this test when connector information persistence is needed in the future.
        /// </remarks>
        [Test]
        public void ConnectorInfoNullAfterSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(1), beltPosInfo);

            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            // コネクター情報付きでアイテムを設定
            // Set item with connector information
            var sourceConnector = CreateInventoryConnector(0, "source-path");
            var goalConnector = CreateInventoryConnector(1, "goal-path");
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.5f,
            };

            // コネクター情報が設定されていることを確認
            // Verify connector information is set
            Assert.IsNotNull(inventoryItems[0].StartConnector, "セーブ前はStartConnectorが設定されているべき / StartConnector should be set before save");
            Assert.IsNotNull(inventoryItems[0].GoalConnector, "セーブ前はGoalConnectorが設定されているべき / GoalConnector should be set before save");
            Assert.AreEqual(sourceConnector.ConnectorGuid, inventoryItems[0].StartConnector.ConnectorGuid);
            Assert.AreEqual(goalConnector.ConnectorGuid, inventoryItems[0].GoalConnector.ConnectorGuid);

            // セーブデータ取得
            // Get save data
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };

            // セーブデータをロード
            // Load save data
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(new BlockInstanceId(1), blockConnector);
            var newBelt = new VanillaBeltConveyorComponent(states, 4, 4000, beltConveyorConnector, BeltConveyorSlopeType.Straight);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);

            // アイテム自体は復元されていることを確認
            // Verify item itself is restored
            Assert.IsNotNull(newInventoryItems[0]);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.5f, newInventoryItems[0].RemainingPercent);

            // 現状の仕様：コネクター情報はnullになる
            // Current specification: Connector information becomes null
            Assert.IsNull(newInventoryItems[0].StartConnector, "ロード後はStartConnectorがnullになる（現状の仕様） / StartConnector becomes null after load (current specification)");
            Assert.IsNull(newInventoryItems[0].GoalConnector, "ロード後はGoalConnectorがnullになる（現状の仕様） / GoalConnector becomes null after load (current specification)");
        }

        private static BlockConnectInfoElement CreateInventoryConnector(int index, string pathId)
        {
            return new BlockConnectInfoElement(index, "Inventory", Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>(), new InventoryConnectOption(pathId));
        }
    }
}
