using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BeltConveyorSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = serviceProvider.GetService<IItemStackFactory>();
            var componentFactory = serviceProvider.GetService<ComponentFactory>();

            var beltPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var belt = new VanillaBeltConveyor(1, 10, 1, 4, 4000, beltPosInfo, componentFactory);
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (BeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt);

            var timeOfItemEnterToExit = belt.TimeOfItemEnterToExit;
            //アイテムを設定
            inventoryItems[0] = new BeltConveyorInventoryItem(1, timeOfItemEnterToExit - 700, 0);
            inventoryItems[2] = new BeltConveyorInventoryItem(2, timeOfItemEnterToExit - 500, 0);
            inventoryItems[3] = new BeltConveyorInventoryItem(5, timeOfItemEnterToExit, 0);

            //セーブデータ取得
            var str = belt.GetSaveState();
            Debug.Log(str);
            //セーブデータをロード
            var newBelt = new VanillaBeltConveyor(1, 10, 1, str, 4, 4000, beltPosInfo, componentFactory);
            var newInventoryItems = (BeltConveyorInventoryItem[])inventoryItemsField.GetValue(newBelt);

            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            Assert.AreEqual(1, newInventoryItems[0].ItemId);
            Assert.AreEqual(timeOfItemEnterToExit - 700, newInventoryItems[0].RemainingTime);
            Assert.AreEqual(2, newInventoryItems[2].ItemId);
            Assert.AreEqual(timeOfItemEnterToExit - 500, newInventoryItems[2].RemainingTime);
            Assert.AreEqual(5, newInventoryItems[3].ItemId);
            Assert.AreEqual(timeOfItemEnterToExit, newInventoryItems[3].RemainingTime);
        }
    }
}