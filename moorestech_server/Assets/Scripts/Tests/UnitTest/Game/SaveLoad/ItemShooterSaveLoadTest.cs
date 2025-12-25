using System.Collections.Generic;
using System.Linq;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var itemShooter = blockFactory.Create(ForUnitTestModBlockId.StraightItemShooter, new BlockInstanceId(1), posInfo);
            
            var shooter = itemShooter.GetComponent<ItemShooterComponent>();
            // コンポーネントサービスを取得する // Acquire internal service instance via reflection
            var serviceField = typeof(ItemShooterComponent).GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance);
            var service = (ItemShooterComponentService)serviceField.GetValue(shooter);
            var inventoryItems = service.EnumerateInventoryItems().ToArray();
            
            //アイテムを設定
            var item1Speed = 1.5f;
            var item2Speed = 2.2f;
            var item3Speed = 5f;
            var item1RemainingPercent = 0.5f;
            var item2RemainingPercent = 0.3f;
            var item3RemainingPercent = 0.0f;
            var item0 = new ShooterInventoryItem(new ItemId(1), new ItemInstanceId(0), item1Speed, null, null) { RemainingPercent = item1RemainingPercent };
            var item2 = new ShooterInventoryItem(new ItemId(2), new ItemInstanceId(0), item2Speed, null, null) { RemainingPercent = item2RemainingPercent };
            var item3 = new ShooterInventoryItem(new ItemId(5), new ItemInstanceId(0), item3Speed, null, null) { RemainingPercent = item3RemainingPercent };
            // SetSlotを利用して初期化 // Seed inventory via service API
            service.SetSlot(0, item0);
            service.SetSlot(2, item2);
            service.SetSlot(3, item3);
            
            
            //セーブデータ取得
            var str = shooter.GetSaveState();
            var states = new Dictionary<string, string>() { { shooter.SaveKey, str } };
            Debug.Log(str);
            
            //セーブデータをロード
            var newShooter = blockFactory.Load(itemShooter.BlockMasterElement.BlockGuid, new BlockInstanceId(0), states, posInfo).GetComponent<ItemShooterComponent>();
            var newService = (ItemShooterComponentService)serviceField.GetValue(newShooter);
            var newInventoryItems = newService.EnumerateInventoryItems().ToArray();
            
            //アイテムが一致するかチェック
            Assert.AreEqual(service.SlotSize, newInventoryItems.Length);
            
            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(item1Speed, newInventoryItems[0].CurrentSpeed);
            Assert.AreEqual(item1RemainingPercent, newInventoryItems[0].RemainingPercent);
            
            Assert.IsTrue(newInventoryItems[1] == null);
            
            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(item2Speed, newInventoryItems[2].CurrentSpeed);
            Assert.AreEqual(item2RemainingPercent, newInventoryItems[2].RemainingPercent);
            
            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(item3Speed, newInventoryItems[3].CurrentSpeed);
            Assert.AreEqual(item3RemainingPercent, newInventoryItems[3].RemainingPercent);
        }
    }
}
