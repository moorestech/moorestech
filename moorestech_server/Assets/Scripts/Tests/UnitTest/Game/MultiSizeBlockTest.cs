using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class MultiSizeBlockTest
    {
        public const int Block_1x4_Id = 9;
        public const int Block_3x2_Id = 10;
        public const int Block_1x2x3_Id = 11;
        
        private IBlockFactory _blockFactory;
        private IWorldBlockDatastore worldDatastore;
        
        [Test]
        public void BlockPlaceAndGetTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            _blockFactory = ServerContext.BlockFactory;
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            //平面設置の検証
            PlaceBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North);
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, 10));
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, 13));
            RetrieveNonExistentBlock(new Vector3Int(9, 0, 10));
            RetrieveNonExistentBlock(new Vector3Int(10, 0, 15));
            
            PlaceBlock(Block_1x4_Id, new Vector3Int(10, 0, -10), BlockDirection.East);
            RetrieveBlock(Block_1x4_Id, new Vector3Int(10, 0, -10));
            RetrieveBlock(Block_1x4_Id, new Vector3Int(13, 0, -10));
            RetrieveNonExistentBlock(new Vector3Int(9, 0, -10));
            RetrieveNonExistentBlock(new Vector3Int(15, 0, -10));
            
            PlaceBlock(Block_3x2_Id, new Vector3Int(-10, 0, -10), BlockDirection.South);
            RetrieveBlock(Block_3x2_Id, new Vector3Int(-10, 0, -10));
            RetrieveBlock(Block_3x2_Id, new Vector3Int(-8, 0, -9));
            RetrieveNonExistentBlock(new Vector3Int(-10, 0, -11));
            RetrieveNonExistentBlock(new Vector3Int(-7, 0, -9));
            
            //立体設置の検証
            PlaceBlock(Block_1x2x3_Id, new Vector3Int(20, 0, 20), BlockDirection.North);
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(20, 0, 20));
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(20, 1, 22));
            RetrieveNonExistentBlock(new Vector3Int(21, 1, 22));
            RetrieveNonExistentBlock(new Vector3Int(20, 2, 22));
            RetrieveNonExistentBlock(new Vector3Int(20, 1, 23));
            
            PlaceBlock(Block_1x2x3_Id, new Vector3Int(40, 0, 40), BlockDirection.UpEast);
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(40, 0, 40));
            RetrieveBlock(Block_1x2x3_Id, new Vector3Int(41, 2, 40));
            RetrieveNonExistentBlock(new Vector3Int(42, 2, 40));
            RetrieveNonExistentBlock(new Vector3Int(41, 3, 40));
            RetrieveNonExistentBlock(new Vector3Int(41, 2, 41));
        }
        
        
        [Test]
        public void OverlappingBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            _blockFactory = ServerContext.BlockFactory;
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            PlaceBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North);
            PlaceBlock(Block_3x2_Id, new Vector3Int(10, 0, 12), BlockDirection.South);
            
            //3x2が設置されてないことをチェックする
            RetrieveBlock(Block_3x2_Id, new Vector3Int(11, 0, 12));
        }
        
        [Test]
        public void BoundaryBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            _blockFactory = ServerContext.BlockFactory;
            worldDatastore = ServerContext.WorldBlockDatastore;
            
            
            PlaceBlock(Block_1x4_Id, new Vector3Int(10, 0, 10), BlockDirection.North);
            PlaceBlock(Block_1x4_Id, new Vector3Int(11, 0, 11), BlockDirection.East);
            
            //1x4が設置されてないことをチェックする
            RetrieveBlock(Block_1x4_Id, new Vector3Int(11, 0, 11));
        }
        
        /// <summary>
        ///     ブロックを設置する
        /// </summary>
        private void PlaceBlock(int blockId, Vector3Int position, BlockDirection direction)
        {
            var posInfo = new BlockPositionInfo(position, direction, Vector3Int.one);
            var block = _blockFactory.Create(blockId, BlockInstanceId.Create(), posInfo);
            
            worldDatastore.TryAddBlock(block);
        }
        
        /// <summary>
        ///     ブロックが設置されていることを確認する
        /// </summary>
        private void RetrieveBlock(int expectedBlockId, Vector3Int position)
        {
            var block = worldDatastore.GetBlock(position);
            Assert.IsNotNull(block);
            Assert.AreEqual(expectedBlockId, block.BlockId);
        }
        
        /// <summary>
        ///     ブロックが設置されていないことを確認する
        /// </summary>
        private void RetrieveNonExistentBlock(Vector3Int position)
        {
            Assert.False(worldDatastore.Exists(position));
        }
    }
}