using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ItemShooterSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var itemShooter = blockFactory.Create(ForUnitTestModBlockId.StraightItemShooter, new BlockInstanceId(1), posInfo);
            
            var shooter = itemShooter.GetComponent<ItemShooterComponent>();
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(ItemShooterComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (ShooterInventoryItem[])inventoryItemsField.GetValue(shooter);
            
            //アイテムを設定
            var item1Speed = 1.5f;
            var item2Speed = 2.2f;
            var item3Speed = 5f;
            var item1RemainingPercent = 0.5f;
            var item2RemainingPercent = 0.3f;
            var item3RemainingPercent = 0.0f;
            inventoryItems[0] = new ShooterInventoryItem(new ItemId(1), new ItemInstanceId(0), item1Speed);
            inventoryItems[0].RemainingPercent = item1RemainingPercent;
            inventoryItems[2] = new ShooterInventoryItem(new ItemId(2), new ItemInstanceId(0), item2Speed);
            inventoryItems[2].RemainingPercent = item2RemainingPercent;
            inventoryItems[3] = new ShooterInventoryItem(new ItemId(5), new ItemInstanceId(0), item3Speed);
            inventoryItems[3].RemainingPercent = item3RemainingPercent;
            
            
            //セーブデータ取得
            var str = shooter.GetSaveState();
            Debug.Log(str);
            
            //セーブデータをロード
            var newShooter = blockFactory.Load(itemShooter.BlockElement.BlockGuid, new BlockInstanceId(0), str, posInfo).GetComponent<ItemShooterComponent>();
            var newInventoryItems = (ShooterInventoryItem[])inventoryItemsField.GetValue(newShooter);
            
            //アイテムが一致するかチェック
            Assert.AreEqual(inventoryItems.Length, newInventoryItems.Length);
            
            Assert.AreEqual(1, newInventoryItems[0].ItemId);
            Assert.AreEqual(item1Speed, newInventoryItems[0].CurrentSpeed);
            Assert.AreEqual(item1RemainingPercent, newInventoryItems[0].RemainingPercent);
            
            Assert.IsTrue(newInventoryItems[1] == null);
            
            Assert.AreEqual(2, newInventoryItems[2].ItemId);
            Assert.AreEqual(item2Speed, newInventoryItems[2].CurrentSpeed);
            Assert.AreEqual(item2RemainingPercent, newInventoryItems[2].RemainingPercent);
            
            Assert.AreEqual(5, newInventoryItems[3].ItemId);
            Assert.AreEqual(item3Speed, newInventoryItems[3].CurrentSpeed);
            Assert.AreEqual(item3RemainingPercent, newInventoryItems[3].RemainingPercent);
        }
    }
}