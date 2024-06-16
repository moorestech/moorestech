using System;
using System.Reflection;
using Core.Const;
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
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemFactory = ServerContext.ItemStackFactory;
            
            // アイテムシューターのテストは、以下のように、一度下がり、再び上がるような構造になっている
            // ↓ チェスト
            // □ ＿ 
            //     ＼         ＿ ＿ → アイテムの流れ
            //        ＼ ＿ ／
            //   ↑  ↑ アイテムシューター
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
            
            chest.InsertItem(itemFactory.Create(1, 1));
            
            // チェストのUpdateをリフレクションで無理やり呼び出し
            chest.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(chest, null);
            
            
            // デフォルトでインサートされる速度の検証
            var shooterItem1 = GetShooterItem(shooter1);
            Assert.AreEqual(1, shooterItem1.ItemId);
            Assert.AreEqual(1, shooterItem1.CurrentSpeed);
            Assert.AreEqual(1, shooterItem1.RemainingPercent);
            
            // 個々の値は実際の値をみて検証し、極端にかわってなければOKとする
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
                if (item.Id != ItemConst.EmptyItemId)
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
        
        private IBlock AddBlock(int blockId, Vector3Int position)
        {
            var blockFactory = ServerContext.BlockFactory;
            var world = ServerContext.WorldBlockDatastore;
            
            var block = blockFactory.Create(blockId, BlockInstanceId.Create(), new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));
            world.AddBlock(block);
            
            return block;
        }
    }
}