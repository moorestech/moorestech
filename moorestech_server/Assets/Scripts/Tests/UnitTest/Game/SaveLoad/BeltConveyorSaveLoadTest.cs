
using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item;
using Game.Block.Blocks.BeltConveyor;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BeltConveyorSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var belt = new VanillaBeltConveyor(1, 10, 1, itemsStackFactory, 4, 4000);
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField =
                typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (List<BeltConveyorInventoryItem>)inventoryItemsField.GetValue(belt);
            //アイテムを設定
            inventoryItems.Add(new BeltConveyorInventoryItem(1, 10, 0, 1));
            inventoryItems.Add(new BeltConveyorInventoryItem(2, 100, 1000, 2));
            inventoryItems.Add(new BeltConveyorInventoryItem(5, 2500, 2000, 3));

            //セーブデータ取得
            var str = belt.GetSaveState();
            Debug.Log(str);
            //セーブデータをロード
            var newBelt = new VanillaBeltConveyor(1, 10, 1, str, itemsStackFactory, 4, 4000);
            var newInventoryItems = (List<BeltConveyorInventoryItem>)inventoryItemsField.GetValue(newBelt);

            //アイテムが一致するかチェック
            Assert.AreEqual(3, newInventoryItems.Count);
            Assert.AreEqual(1, newInventoryItems[0].ItemId);
            Assert.AreEqual(10, newInventoryItems[0].RemainingTime);
            Assert.AreEqual(0, newInventoryItems[0].LimitTime);
            Assert.AreEqual(2, newInventoryItems[1].ItemId);
            Assert.AreEqual(100, newInventoryItems[1].RemainingTime);
            Assert.AreEqual(1000, newInventoryItems[1].LimitTime);
            Assert.AreEqual(5, newInventoryItems[2].ItemId);
            Assert.AreEqual(2500, newInventoryItems[2].RemainingTime);
            Assert.AreEqual(2000, newInventoryItems[2].LimitTime);
        }
    }
}