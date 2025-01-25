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

namespace Tests.UnitTest.Game
{
    public class MapObjectMinerTest
    {
        [Test]
        public void MapObjectMinerMiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMapObjectMiner, Vector3Int.zero, BlockDirection.North, out var block);
            
            // 1秒間採掘
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(1))
            {
                GameUpdater.UpdateWithWait();
            }
            
            // まだインベントリに木がないことを確認する
            var blockInChestComponent = block.GetComponent<VanillaChestComponent>();
            Assert.AreEqual(0, blockInChestComponent.InventoryItems[0].Id);
            
            // 1秒間採掘
            startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(1.1)) // 余裕を持って1.1秒間待つ
            {
                GameUpdater.UpdateWithWait();
            }
            
            // インベントリに木が入っていることを確認する
            Assert.AreEqual(1, blockInChestComponent.InventoryItems[0].Id);
            Assert.AreEqual(1, blockInChestComponent.InventoryItems[0].Count);
        }
    }
}