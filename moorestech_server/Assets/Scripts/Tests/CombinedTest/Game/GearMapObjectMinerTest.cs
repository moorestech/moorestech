using System;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class GearMapObjectMinerTest
    {
        [Test]
        public void MapObjectMinerMiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var gearMapObjectMinerPosition = Vector3Int.zero;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMapObjectMiner, gearMapObjectMinerPosition, BlockDirection.North, out var block);
            var infinityTorqueSimpleGearGeneratorPosition = new Vector3Int(1, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, infinityTorqueSimpleGearGeneratorPosition, BlockDirection.North, out _);
            
            // 1秒間採掘 採掘時間は2秒に設定している
            // Mining for 1 second. The mining time is set to 2 seconds.
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(1))
            {
                GameUpdater.UpdateWithWait();
            }
            
            // まだインベントリに木がないことを確認する
            // Make sure there is no wood in the inventory yet
            var blockInChestComponent = block.GetComponent<VanillaChestComponent>();
            Assert.AreEqual(0, (int)blockInChestComponent.InventoryItems[0].Id);
            
            // 1秒間採掘
            // Mining for 1 second
            startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(1.1)) // 余裕を持って1.1秒間待つ。Wait for 1.1 seconds with plenty of time
            {
                GameUpdater.UpdateWithWait();
            }
            
            // インベントリにアイテムが入っていることを確認する
            // Make sure there is wood in the inventory
            Assert.AreEqual(2, (int)blockInChestComponent.InventoryItems[0].Id);
            
            // 範囲内にmap objectが2つあるのでアイテムが2個ある事をチェックする
            // There are two map objects in the range, so check that there are two items
            Assert.AreEqual(2, (int)blockInChestComponent.InventoryItems[0].Count);
            
            // 2秒間採掘
            // Mining for 2 seconds
            startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(2.1)) // 余裕を持って2.1秒間待つ。Wait for 2.1 seconds with plenty of time
            {
                GameUpdater.UpdateWithWait();
            }
            
            // インベントリのアイテムが増えていることを確認する
            // Make sure the inventory items have increased
            Assert.AreEqual(4, (int)blockInChestComponent.InventoryItems[0].Count);
        }
    }
}