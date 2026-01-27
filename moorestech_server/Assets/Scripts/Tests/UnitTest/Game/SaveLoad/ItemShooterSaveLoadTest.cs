using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
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

            // マスターデータからtotalTicksを取得
            // Get totalTicks from master data
            var shooterParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.StraightItemShooter).BlockParam as ItemShooterBlockParam;
            var transitSeconds = 1.0 / shooterParam.ItemShootSpeed;
            var totalTicks = GameUpdater.SecondsToTicks(transitSeconds);

            // アイテムを設定（tick化によりスピードは廃止、残りtickで管理）
            // Set items (speed removed after tick conversion, managed by remaining ticks)
            var item1RemainingTicks = (uint)(totalTicks * 0.5);
            var item2RemainingTicks = (uint)(totalTicks * 0.3);
            var item3RemainingTicks = 0u;
            var item0 = new ShooterInventoryItem(new ItemId(1), new ItemInstanceId(0), totalTicks, null, null) { RemainingTicks = item1RemainingTicks };
            var item2 = new ShooterInventoryItem(new ItemId(2), new ItemInstanceId(0), totalTicks, null, null) { RemainingTicks = item2RemainingTicks };
            var item3 = new ShooterInventoryItem(new ItemId(5), new ItemInstanceId(0), totalTicks, null, null) { RemainingTicks = item3RemainingTicks };
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
            
            // アイテムが一致するかチェック
            // Check that items match
            Assert.AreEqual(service.SlotSize, newInventoryItems.Length);

            Assert.AreEqual(1, newInventoryItems[0].ItemId.AsPrimitive());
            Assert.AreEqual(item1RemainingTicks, newInventoryItems[0].RemainingTicks);
            Assert.AreEqual(totalTicks, newInventoryItems[0].TotalTicks);

            Assert.IsTrue(newInventoryItems[1] == null);

            Assert.AreEqual(2, newInventoryItems[2].ItemId.AsPrimitive());
            Assert.AreEqual(item2RemainingTicks, newInventoryItems[2].RemainingTicks);
            Assert.AreEqual(totalTicks, newInventoryItems[2].TotalTicks);

            Assert.AreEqual(5, newInventoryItems[3].ItemId.AsPrimitive());
            Assert.AreEqual(item3RemainingTicks, newInventoryItems[3].RemainingTicks);
            Assert.AreEqual(totalTicks, newInventoryItems[3].TotalTicks);
        }
    }
}
