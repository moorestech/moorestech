using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
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
            
            
            // アイテムシューターは、以下のように、一度下がり、再び上がるような構造になっている
            // ↓ チェスト　 
            // □ ＿ 　　　　 ＿ ＿ → アイテムの流れ
            //     ＼ ＿ ／
            //   ↑  ↑ アイテムシューター
            var chestPosition = new Vector3Int(0,0, 0);
            var horizonShooter1 = new Vector3Int(0, 0, 1);
            var downShooter = new Vector3Int(0, -1, 2);
            var horizonShooter2 = new Vector3Int(0, -1, 3);
            var upShooter = new Vector3Int(0, 0, 4);
            var horizonShooter3 = new Vector3Int(0, 0, 5);
            var horizonShooter4 = new Vector3Int(0, 0, 6);
            
            var chest = AddBlock(ForUnitTestModBlockId.ChestId, chestPosition);
            var shooter1 = AddBlock(ForUnitTestModBlockId.ItemShooter, horizonShooter1).GetComponent<ItemShooterComponent>();
            var down = AddBlock(ForUnitTestModBlockId.ItemShooter, downShooter).GetComponent<ItemShooterComponent>();
            var shooter2 = AddBlock(ForUnitTestModBlockId.ItemShooter, horizonShooter2).GetComponent<ItemShooterComponent>();
            var up = AddBlock(ForUnitTestModBlockId.ItemShooter, upShooter).GetComponent<ItemShooterComponent>();
            var shooter3 = AddBlock(ForUnitTestModBlockId.ItemShooter, horizonShooter3).GetComponent<ItemShooterComponent>();
            var shooter4 = AddBlock(ForUnitTestModBlockId.ItemShooter, horizonShooter4).GetComponent<ItemShooterComponent>();
            
            
            
            // 上方向は速度が低下し、ゼロになると止まる
            
            
            // 下方向に加速する
            // 平行だと速度が低下する
            
            // デフォルトでインサートされる速度
            
        }
        
        private IBlock AddBlock(int blockId, Vector3Int position)
        {
            var blockFactory = ServerContext.BlockFactory;
            var world = ServerContext.WorldBlockDatastore;
            
            var block = blockFactory.Create(blockId, new BlockInstanceId(10), new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));
            world.AddBlock(block);
            
            return block;
        }
    }
}