using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class FastBeltConveyorTest
    {
        /// <summary>
        /// TODO 1フレームで3ブロック進むベルトコンベアのテスト
        /// TODO Testing a conveyor belt that moves 3 blocks per frame
        /// </summary>
        public void OneFramePer3BlockBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);

            //PlaceBlock(ForUnitTestModBlockId.FastBeltConveyor, new Vector3Int(0, 0, 0));
            //PlaceBlock(ForUnitTestModBlockId.FastBeltConveyor, new Vector3Int(0, 0, 1));
            //PlaceBlock(ForUnitTestModBlockId.FastBeltConveyor, new Vector3Int(0, 0, 2));
            
            GameUpdater.SpecifiedDeltaTimeUpdate(1);
        }
        
        IBlock PlaceBlock(BlockId blockId, Vector3Int position)
        {
            var world = ServerContext.WorldBlockDatastore;
            
            world.TryAddBlock(blockId, position, BlockDirection.North, out var block);
            return block;
        }
    }
}