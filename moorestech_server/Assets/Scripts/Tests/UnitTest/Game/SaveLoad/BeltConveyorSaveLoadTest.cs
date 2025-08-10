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
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
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
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0))
            {
                RemainingPercent = 0.3f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0))
            {
                RemainingPercent = 0.5f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0))
            {
                RemainingPercent = 1f,
            };
            
            
            //セーブデータ取得
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };
            Debug.Log(str);
            
            
            //セーブデータをロード
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(blockConnector);
            
            var newBelt = new VanillaBeltConveyorComponent(states, 4, 4000, beltConveyorConnector, BeltConveyorSlopeType.Straight);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);
            
            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.3f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.5f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(1f, newInventoryItems[3].RemainingPercent);
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
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(0))
            {
                RemainingPercent = 0.3f,
            };
            inventoryItems[2] = new VanillaBeltConveyorInventoryItem(new ItemId(2), new ItemInstanceId(0))
            {
                RemainingPercent = 0.5f,
            };
            inventoryItems[3] = new VanillaBeltConveyorInventoryItem(new ItemId(5), new ItemInstanceId(0))
            {
                RemainingPercent = 1f,
            };
            
            
            //セーブデータ取得
            var str = belt.GetSaveState();
            var states = new Dictionary<string, string>() { { belt.SaveKey, str } };
            Debug.Log(str);
            
            
            //セーブデータをロード
            var blockConnector = new BlockConnectorComponent<IBlockInventory>(null, null, beltPosInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(blockConnector); 
            
            var newBelt = new VanillaBeltConveyorComponent(states, 4, 4000, beltConveyorConnector, BeltConveyorSlopeType.Straight);
            var newInventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);
            
            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(0.3f, newInventoryItems[0].RemainingPercent);
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(0.5f, newInventoryItems[2].RemainingPercent);
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(1f, newInventoryItems[3].RemainingPercent);
        }
    }
}