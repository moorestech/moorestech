#if NET6_0
using System;
using Core.Const;
using Core.Util;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Player;
using Test.Module.TestMod;

namespace Test.UnitTest.Server.Player
{
    public class CoordinateToChunkBlocksTest
    {
        private IBlockFactory _blockFactory;

        [Test]
        public void NothingBlockTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            var b = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(new CoreVector2Int(0, 0), worldData);

            Assert.AreEqual(b.GetLength(0), ChunkResponseConst.ChunkSize);
            Assert.AreEqual(b.GetLength(1), ChunkResponseConst.ChunkSize);

            for (var i = 0; i < b.GetLength(0); i++)
            for (var j = 0; j < b.GetLength(1); j++)
                Assert.AreEqual(BlockConst.EmptyBlockId, b[i, j]);
        }

        [Test]
        public void SameBlockResponseTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            var random = new Random(3944156);
            //ブロックの設置
            for (var i = 0; i < 10000; i++)
            {
                var b = CreateMachine(random.Next(1, 500));
                worldData.AddBlock(b, random.Next(-300, 300), random.Next(-300, 300), BlockDirection.North);
            }

            //レスポンスのチェック
            for (var l = 0; l < 100; l++)
            {
                var c = new CoreVector2Int(
                    random.Next(-5, 5) * ChunkResponseConst.ChunkSize,
                    random.Next(-5, 5) * ChunkResponseConst.ChunkSize);
                var b = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(c, worldData);

                //ブロックの確認
                for (var i = 0; i < b.GetLength(0); i++)
                for (var j = 0; j < b.GetLength(1); j++)
                    Assert.AreEqual(
                        worldData.GetBlock(c.X + i, c.Y + j).BlockId,
                        b[i, j]);
            }
        }

        private IBlock CreateMachine(int id)
        {
            if (_blockFactory == null)
            {
                var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
                _blockFactory = serviceProvider.GetService<IBlockFactory>();
            }

            return _blockFactory.Create(id, CreateBlockEntityId.Create());
        }
    }
}
#endif