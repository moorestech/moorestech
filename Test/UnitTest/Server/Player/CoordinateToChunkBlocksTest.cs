using System;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Block.RecipeConfig;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.PacketHandle.PacketResponse.Player;
using Server.Protocol.PacketResponse.Const;
using Test.Module.TestConfig;
using EntityId = Game.World.Interface.Util.EntityId;

namespace Test.UnitTest.Server.Player
{
    public class CoordinateToChunkBlocksTest
    {
        [Test]
        public void NothingBlockTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            var b = CoordinateToChunkBlocks.Convert(new Coordinate(0, 0), worldData);

            Assert.AreEqual(b.GetLength(0), ChunkResponseConst.ChunkSize);
            Assert.AreEqual(b.GetLength(1), ChunkResponseConst.ChunkSize);

            for (int i = 0; i < b.GetLength(0); i++)
            {
                for (int j = 0; j < b.GetLength(1); j++)
                {
                    Assert.AreEqual(BlockConst.EmptyBlockId, b[i, j]);
                }
            }
        }

        [Test]
        public void SameBlockResponseTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            var random = new Random(3944156);
            //ブロックの設置
            for (int i = 0; i < 10000; i++)
            {
                var b = CreateMachine(random.Next(0, 500));
                worldData.AddBlock(b, random.Next(-300, 300), random.Next(-300, 300), BlockDirection.North);
            }

            //レスポンスのチェック
            for (int l = 0; l < 100; l++)
            {
                var c = new Coordinate(
                    random.Next(-5, 5) * ChunkResponseConst.ChunkSize,
                    random.Next(-5, 5) * ChunkResponseConst.ChunkSize);
                var b = CoordinateToChunkBlocks.Convert(c, worldData);

                //ブロックの確認
                for (int i = 0; i < b.GetLength(0); i++)
                {
                    for (int j = 0; j < b.GetLength(1); j++)
                    {
                        Assert.AreEqual(
                            worldData.GetBlock(c.X + i, c.Y + j).GetBlockId(),
                            b[i, j]);
                    }
                }
            }
        }


        private BlockFactory _blockFactory;

        private VanillaMachine CreateMachine(int id)
        {
            if (_blockFactory == null)
            {
                var itemStackFactory = new ItemStackFactory(new TestItemConfig());
                _blockFactory = new BlockFactory(new AllMachineBlockConfig(),
                    new VanillaIBlockTemplates(new TestMachineRecipeConfig(itemStackFactory), itemStackFactory));
            }

            var machine = _blockFactory.Create(id, EntityId.NewEntityId()) as VanillaMachine;
            return machine;
        }
    }
}