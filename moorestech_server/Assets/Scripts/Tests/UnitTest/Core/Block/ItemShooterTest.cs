using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Core.Block
{
    public class ItemShooterTest
    {
        [Test]
        public void ShooterTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var itemFactory = ServerContext.ItemStackFactory;
            
            // アイテムシューターのテストは、以下のように、一度下がり、再び上がるような構造になっている
            // Item shooter tests are structured to drop once and then rise again as follows
            // ↓ チェスト Chest
            // □ ＿ 
            //     ＼         ＿ ＿ → アイテムの流れ Item flow
            //        ＼ ＿ ／
            //   ↑  ↑ アイテムシューター ItemShooter
            var chestPosition = new Vector3Int(0, 0, 0);
            var horizonShooter1 = new Vector3Int(0, 0, 1);
            var downShooter1 = new Vector3Int(0, -1, 2);
            var downShooter2 = new Vector3Int(0, -2, 3);
            var horizonShooter2 = new Vector3Int(0, -2, 4);
            var upShooter = new Vector3Int(0, -2, 5);
            var horizonShooter3 = new Vector3Int(0, -1, 6);
            var horizonShooter4 = new Vector3Int(0, -1, 7);
            
            var chest = AddBlock(ForUnitTestModBlockId.ChestId, chestPosition).GetComponent<VanillaChestComponent>();
            var shooter1 = AddBlock(ForUnitTestModBlockId.StraightItemShooter, horizonShooter1).GetComponent<ItemShooterComponent>();
            var down1 = AddBlock(ForUnitTestModBlockId.DownItemShooter, downShooter1).GetComponent<ItemShooterComponent>();
            var down2 = AddBlock(ForUnitTestModBlockId.DownItemShooter, downShooter2).GetComponent<ItemShooterComponent>();
            var shooter2 = AddBlock(ForUnitTestModBlockId.StraightItemShooter, horizonShooter2).GetComponent<ItemShooterComponent>();
            var up = AddBlock(ForUnitTestModBlockId.UpItemShooter, upShooter).GetComponent<ItemShooterComponent>();
            var shooter3 = AddBlock(ForUnitTestModBlockId.StraightItemShooter, horizonShooter3).GetComponent<ItemShooterComponent>();
            var shooter4 = AddBlock(ForUnitTestModBlockId.StraightItemShooter, horizonShooter4).GetComponent<ItemShooterComponent>();
            
            chest.InsertItem(itemFactory.Create(new ItemId(1), 1));
            
            // チェストのUpdateを呼び出し
            // Call the Update of the chest
            chest.Update();
            
            // デフォルトでインサートされる速度の検証
            // Verification of the speed inserted by default
            var shooterItem1 = GetShooterItem(shooter1);
            Assert.AreEqual(1, shooterItem1.ItemId.AsPrimitive());
            Assert.AreEqual(1, shooterItem1.CurrentSpeed);
            Assert.AreEqual(1, shooterItem1.RemainingPercent);
            
            // 個々の値は実際の値をみて検証し、極端にかわってなければOKとする
            // Verify each value by looking at the actual value and OK if it is not extremely different
            var shootedItem = WaitInsertItem(down1, "1");
            Assert.IsTrue(0.7f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 0.8f);
            
            shootedItem = WaitInsertItem(down2, "Down1");
            Assert.IsTrue(1.2f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 1.5f);
            
            shootedItem = WaitInsertItem(shooter2, "Down2");
            Assert.IsTrue(1.7f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 2.0f);
            
            shootedItem = WaitInsertItem(up, "2");
            Assert.IsTrue(1.4f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 1.8f);
            
            shootedItem = WaitInsertItem(shooter3, "Up", up);
            Assert.IsTrue(0.9f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 1.3f);
            
            shootedItem = WaitInsertItem(shooter4, "3");
            Assert.IsTrue(0.6f <= shootedItem.CurrentSpeed && shootedItem.CurrentSpeed <= 1.1f);
        }
        
        private ShooterInventoryItem WaitInsertItem(ItemShooterComponent waitTarget, string tag, ItemShooterComponent waitFrom = null)
        {
            var currentTime = DateTime.Now;
            while (true)
            {
                var item = waitTarget.GetItem(0);
                if (item.Id != ItemMaster.EmptyItemId)
                {
                    // アイテム挿入時間を出力
                    var shooterItem = GetShooterItem(waitTarget);
                    var totalSeconds = (DateTime.Now - currentTime).TotalSeconds;
                    var currentSpeed = shooterItem.CurrentSpeed;
                    // 下2桁表示
                    Debug.Log($"{tag} Time: {totalSeconds:F2} Speed: {currentSpeed:F2}");
                    return shooterItem;
                }
                GameUpdater.Update();
                
                // 5秒経過したら失敗
                if ((DateTime.Now - currentTime).TotalSeconds > 5)
                {
                    Assert.Fail("インサートができていません");
                }
            }
        }
        
        private ShooterInventoryItem GetShooterItem(ItemShooterComponent target)
        {
            var item = target.BeltConveyorItems[0];
            return item as ShooterInventoryItem;
        }
        
        private IBlock AddBlock(BlockId blockId, Vector3Int position)
        {
            var world = ServerContext.WorldBlockDatastore;
            
            world.TryAddBlock(blockId, position, BlockDirection.North, out var block);
            
            return block;
        }
    }
}