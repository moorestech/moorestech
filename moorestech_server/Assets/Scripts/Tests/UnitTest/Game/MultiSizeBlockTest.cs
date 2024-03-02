using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.UnitTest.Game
{
    public class MultiSizeBlockTest
    {
        public const int Block_1x4_Id = 9;
        public const int Block_3x2_Id = 10;

        private IBlockFactory _blockFactory;
        private IWorldBlockDatastore worldDatastore;

        [Test]
        public void BlockPlaceAndGetTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            _blockFactory = serviceProvider.GetService<IBlockFactory>();
            worldDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            PlaceBlock(Block_1x4_Id, new Vector2Int(10, 10), BlockDirection.North);
            RetrieveBlock(Block_1x4_Id, new Vector2Int(10, 10));
            RetrieveBlock(Block_1x4_Id, new Vector2Int(10, 13));
            RetrieveNonExistentBlock(new Vector2Int(9, 10));
            RetrieveNonExistentBlock(new Vector2Int(10, 15));

            PlaceBlock(Block_1x4_Id, new Vector2Int(10, -10), BlockDirection.East);
            RetrieveBlock(Block_1x4_Id, new Vector2Int(10, -10));
            RetrieveBlock(Block_1x4_Id, new Vector2Int(13, -10));
            RetrieveNonExistentBlock(new Vector2Int(9, -10));
            RetrieveNonExistentBlock(new Vector2Int(15, -10));


            PlaceBlock(Block_3x2_Id, new Vector2Int(-10, -10), BlockDirection.South);
            RetrieveBlock(Block_3x2_Id, new Vector2Int(-10, -10));
            RetrieveBlock(Block_3x2_Id, new Vector2Int(-8, -9));
            RetrieveNonExistentBlock(new Vector2Int(-10, -11));
            RetrieveNonExistentBlock(new Vector2Int(-7, -9));


            PlaceBlock(Block_3x2_Id, new Vector2Int(-10, 10), BlockDirection.West);
            RetrieveBlock(Block_3x2_Id, new Vector2Int(-10, 10));
            RetrieveBlock(Block_3x2_Id, new Vector2Int(-9, 12));
            RetrieveNonExistentBlock(new Vector2Int(-10, 9));
            RetrieveNonExistentBlock(new Vector2Int(-9, 13));
        }


        [Test]
        public void OverlappingBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            _blockFactory = serviceProvider.GetService<IBlockFactory>();
            worldDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            PlaceBlock(Block_1x4_Id, new Vector2Int(10, 10), BlockDirection.North);
            PlaceBlock(Block_3x2_Id, new Vector2Int(10, 12), BlockDirection.South);

            //3x2が設置されてないことをチェックする
            RetrieveBlock(Block_3x2_Id, new Vector2Int(11, 12));
        }

        [Test]
        public void BoundaryBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            _blockFactory = serviceProvider.GetService<IBlockFactory>();
            worldDatastore = serviceProvider.GetService<IWorldBlockDatastore>();


            PlaceBlock(Block_1x4_Id, new Vector2Int(10, 10), BlockDirection.North);
            PlaceBlock(Block_1x4_Id, new Vector2Int(11, 11), BlockDirection.East);

            //1x4が設置されてないことをチェックする
            RetrieveBlock(Block_1x4_Id, new Vector2Int(11, 11));
        }

        private void PlaceBlock(int blockId, Vector2Int position, BlockDirection direction)
        {
            var block = _blockFactory.Create(blockId, new Random().Next(0, 10000));
            worldDatastore.AddBlock(block, position.x, position.y, direction);
        }

        private void RetrieveBlock(int expectedBlockId, Vector2Int position)
        {
            var block = worldDatastore.GetBlock(position.x, position.y);
            Assert.IsNotNull(block);
            Assert.AreEqual(expectedBlockId, block.BlockId);
        }

        private void RetrieveNonExistentBlock(Vector2Int position)
        {
            Assert.False(worldDatastore.Exists(position.x, position.y));
        }
    }
}