#if NET6_0
using System;
using Core.Util;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
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
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            _blockFactory = serviceProvider.GetService<IBlockFactory>();
            worldDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            PlaceBlock(Block_1x4_Id, new CoreVector2Int(10, 10), BlockDirection.North);
            RetrieveBlock(Block_1x4_Id, new CoreVector2Int(10, 10));
            RetrieveBlock(Block_1x4_Id, new CoreVector2Int(10, 13));
            RetrieveNonExistentBlock(new CoreVector2Int(9, 10));
            RetrieveNonExistentBlock(new CoreVector2Int(10, 15));
            
            PlaceBlock(Block_1x4_Id, new CoreVector2Int(10, -10), BlockDirection.East);
            RetrieveBlock(Block_1x4_Id, new CoreVector2Int(10, -10));
            RetrieveBlock(Block_1x4_Id, new CoreVector2Int(13, -10));
            RetrieveNonExistentBlock( new CoreVector2Int(9, -10));
            RetrieveNonExistentBlock( new CoreVector2Int(15, -10));
            
            
            PlaceBlock(Block_3x2_Id, new CoreVector2Int(-10, -10), BlockDirection.South);
            RetrieveBlock(Block_3x2_Id, new CoreVector2Int(-10, -10));
            RetrieveBlock(Block_3x2_Id, new CoreVector2Int(-8,-9));
            RetrieveNonExistentBlock(new CoreVector2Int(-10, -11));
            RetrieveNonExistentBlock(new CoreVector2Int(-7, -9));
            
            
            PlaceBlock(Block_3x2_Id, new CoreVector2Int(-10, 10), BlockDirection.West);
            RetrieveBlock(Block_3x2_Id, new CoreVector2Int(-10, 10));
            RetrieveBlock(Block_3x2_Id, new CoreVector2Int(-9, 12));
            RetrieveNonExistentBlock(new CoreVector2Int(-10, 9));
            RetrieveNonExistentBlock(new CoreVector2Int(-9, 13));
        }


        [Test]
        private void DuplicateBlockTest()
        {
            throw new NotImplementedException();
        }

        private void PlaceBlock(int blockId, CoreVector2Int position, BlockDirection direction)
        {
            var block = _blockFactory.Create(blockId,new Random().Next(0,10000) );
            worldDatastore.AddBlock(block, position.X, position.Y, direction);
        }

        private void RetrieveBlock(int expectedBlockId, CoreVector2Int position)
        {
            var block = worldDatastore.GetBlock(position.X, position.Y);
            Assert.IsNotNull(block);
            Assert.AreEqual(expectedBlockId, block.BlockId);
        }

        private void RetrieveNonExistentBlock(CoreVector2Int position)
        {
            Assert.False(worldDatastore.Exists(position.X, position.Y));
        }
    }
}
#endif