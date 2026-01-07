using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>
    /// ベルトコンベアのセーブロードテスト
    /// BeltConveyor save/load test
    /// </summary>
    public class BeltConveyorSaveLoadTest
    {
        [Test]
        public void NormalSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(1), beltPosInfo);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId);
            var guid = blockMaster.BlockGuid;
            var beltParam = blockMaster.BlockParam as BeltConveyorBlockParam;

            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // リフレクションで_inventoryItemsを取得
            // Get _inventoryItems via reflection
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            // ブロックの実際のコネクタを使用してアイテムを設定
            // Set items using the block's actual connectors
            var inputConnects = beltParam.InventoryConnectors.InputConnects.items;
            var outputConnects = beltParam.InventoryConnectors.OutputConnects.items;
            var sourceConnector = inputConnects[0];
            var goalConnector = outputConnects[0];

            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.9f,
            };

            // セーブデータ取得
            // Get save data
            var states = new Dictionary<string, string> { { belt.SaveKey, belt.GetSaveState() } };
            Debug.Log(states[belt.SaveKey]);

            // セーブデータをロード（異なるBlockInstanceIdを使用）
            // Load save data (use different BlockInstanceId)
            var loadedBeltConveyor = blockFactory.Load(guid, new BlockInstanceId(2), states, beltPosInfo);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(loadedBeltConveyor.GetComponent<VanillaBeltConveyorComponent>());

            // アイテムが一致するかチェック
            // Check that items match
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.8f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.85f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(0.9f, newInventoryItems[3].RemainingPercent);

            // コネクタGuidが復元されているかチェック
            // Verify connector Guid is restored
            Assert.IsNotNull(newInventoryItems[0].GoalConnector);
            Assert.IsNotNull(newInventoryItems[2].GoalConnector);
            Assert.IsNotNull(newInventoryItems[3].GoalConnector);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[0].GoalConnector.ConnectorGuid);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[2].GoalConnector.ConnectorGuid);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[3].GoalConnector.ConnectorGuid);
        }

        [Test]
        public void GearSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.GearBeltConveyor, new BlockInstanceId(1), beltPosInfo);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor);
            var guid = blockMaster.BlockGuid;
            var gearBeltParam = blockMaster.BlockParam as GearBeltConveyorBlockParam;

            var belt = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // リフレクションで_inventoryItemsを取得
            // Get _inventoryItems via reflection
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            // ブロックの実際のコネクタを使用してアイテムを設定
            // Set items using the block's actual connectors
            var inputConnects = gearBeltParam.InventoryConnectors.InputConnects.items;
            var outputConnects = gearBeltParam.InventoryConnectors.OutputConnects.items;
            var sourceConnector = inputConnects[0];
            var goalConnector = outputConnects[0];

            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.8f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.85f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0), sourceConnector, goalConnector)
            {
                RemainingPercent = 0.9f,
            };

            // セーブデータ取得
            // Get save data
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };
            Debug.Log(str);

            // セーブデータをロード（異なるBlockInstanceIdを使用してGearNetworkの重複登録を避ける）
            // Load save data (use different BlockInstanceId to avoid duplicate registration in GearNetwork)
            var loadedBeltConveyor = blockFactory.Load(guid, new BlockInstanceId(2), states, beltPosInfo);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(loadedBeltConveyor.GetComponent<VanillaBeltConveyorComponent>());

            // アイテムが一致するかチェック
            // Check that items match
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.8f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.85f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(0.9f, newInventoryItems[3].RemainingPercent);

            // コネクタGuidが復元されているかチェック
            // Verify connector Guid is restored
            Assert.IsNotNull(newInventoryItems[0].GoalConnector);
            Assert.IsNotNull(newInventoryItems[2].GoalConnector);
            Assert.IsNotNull(newInventoryItems[3].GoalConnector);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[0].GoalConnector.ConnectorGuid);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[2].GoalConnector.ConnectorGuid);
            Assert.AreEqual(goalConnector.ConnectorGuid, newInventoryItems[3].GoalConnector.ConnectorGuid);
        }
    }
}
