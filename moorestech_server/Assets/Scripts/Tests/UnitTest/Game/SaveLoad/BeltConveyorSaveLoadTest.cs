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
            var guid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockGuid;
            
            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);
            
            //アイテムを設定
            var item0GoalGuid = Guid.NewGuid();
            var item2GoalGuid = Guid.NewGuid();
            var item3GoalGuid = Guid.NewGuid();
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), CreateInventoryConnector(0, Guid.NewGuid()), CreateInventoryConnector(1, item0GoalGuid))
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), CreateInventoryConnector(2, Guid.NewGuid()), CreateInventoryConnector(3, item2GoalGuid))
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), CreateInventoryConnector(4, Guid.NewGuid()), CreateInventoryConnector(5, item3GoalGuid))
            {
                RemainingPercent = 0.9f,
            };


            //セーブデータ取得
            var states = new Dictionary<string, string> { { belt.SaveKey, belt.GetSaveState() } };
            Debug.Log(states[belt.SaveKey]);

            
            //セーブデータをロード
            var loadedBeltConveyor = blockFactory.Load(guid, new BlockInstanceId(1), states, beltPosInfo);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(loadedBeltConveyor.GetComponent<VanillaBeltConveyorComponent>());

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
            var guid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;

            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            //アイテムを設定
            var item0GoalGuid = Guid.NewGuid();
            var item2GoalGuid = Guid.NewGuid();
            var item3GoalGuid = Guid.NewGuid();
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), CreateInventoryConnector(0, Guid.NewGuid()), CreateInventoryConnector(1, item0GoalGuid))
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), CreateInventoryConnector(2, Guid.NewGuid()), CreateInventoryConnector(3, item2GoalGuid))
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), CreateInventoryConnector(4, Guid.NewGuid()), CreateInventoryConnector(5, item3GoalGuid))
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
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(10, item0GoalGuid), CreateInventoryConnector(11, Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(12, item2GoalGuid), CreateInventoryConnector(13, Guid.NewGuid()), null));
            connectInventory.Add(new DummyBlockInventory(), new ConnectedInfo(CreateInventoryConnector(14, item3GoalGuid), CreateInventoryConnector(15, Guid.NewGuid()), null));
            
            var loadedBeltConveyor = blockFactory.Load(guid, new BlockInstanceId(1), states, beltPosInfo);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(loadedBeltConveyor.GetComponent<VanillaBeltConveyorComponent>());

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
            Assert.AreEqual(item0GoalGuid, newInventoryItems[0].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item2GoalGuid, newInventoryItems[2].GoalConnector.ConnectorGuid);
            Assert.AreEqual(item3GoalGuid, newInventoryItems[3].GoalConnector.ConnectorGuid);
        }

        private static BlockConnectInfoElement CreateInventoryConnector(int index, Guid connectorGuid)
        {
            return new BlockConnectInfoElement(index, "Inventory", connectorGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), null);
        }
    }
}
