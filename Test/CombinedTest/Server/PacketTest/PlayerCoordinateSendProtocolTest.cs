using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Block.RecipeConfig;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server;
using Server.Protocol.PacketResponse.Const;
using Server.Util;
using Test.Module.TestConfig;
using IntId = Game.World.Interface.Util.IntId;

namespace Test.CombinedTest.Server.PacketTest
{
    [TestClass]
    public class PlayerCoordinateSendProtocolTest
    {
        [TestMethod]
        public void SimpleChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            //1回のレスポンスのテスト
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25, response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                {
                    ans.Add(new Coordinate(i, j));
                }
            }

            foreach (var r in response)
            {
                //座標の確認
                Assert.IsTrue(ans.Contains(r.Coordinate));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(BlockConst.EmptyBlockId, r.Blocks[i, j]);
                    }
                }
            }

            //2回目は何も返ってこないテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count, 0);


            //場所をずらしたら返ってくるテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 25, 25))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count, 9);

            //他の名前は普通に取得できるテスト
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(15, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25, response.Count());
        }


        //ブロックを設置するテスト
        [TestMethod]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();

            var random = new Random(13944156);
            //ブロックの設置
            var b = CreateMachine(5);
            worldBlock.AddBlock(b, 0, 0, BlockDirection.North);

            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(20, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25, response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                {
                    ans.Add(new Coordinate(i, j));
                }
            }

            foreach (var r in response)
            {
                //座標の確認
                var c = r.Coordinate;
                Assert.IsTrue(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(worldBlock.GetBlock(c.X + i, c.Y + j).GetBlockId()
                            , r.Blocks[i, j]);
                    }
                }
            }
        }

        //ランダムにブロックを設置するテスト
        [TestMethod]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();

            var random = new Random(13944156);
            //ブロックの設置
            for (int i = 0; i < 1000; i++)
            {
                VanillaMachine b = null;
                if (random.Next(0, 3) == 1)
                {
                    b = CreateMachine(random.Next(short.MaxValue, int.MaxValue));
                }
                else
                {
                    b = CreateMachine(random.Next(0, 500));
                }

                worldBlock.AddBlock(b, random.Next(-300, 300), random.Next(-300, 300), BlockDirection.North);
            }

            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(25, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25, response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                {
                    ans.Add(new Coordinate(i, j));
                }
            }

            foreach (var r in response)
            {
                //座標の確認
                var c = r.Coordinate;
                Assert.IsTrue(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(worldBlock.GetBlock(c.X + i, c.Y + j).GetBlockId()
                            , r.Blocks[i, j]);
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

            var machine = _blockFactory.Create(id, IntId.NewIntId()) as VanillaMachine;
            return machine;
        }

        List<byte> PlayerCoordinatePayload(int playerId, float x, float y)
        {
            var p = new List<byte>();
            p.AddRange(ToByteList.Convert((short) 2));
            p.AddRange(ToByteList.Convert(x));
            p.AddRange(ToByteList.Convert(y));
            p.AddRange(ToByteList.Convert(playerId));
            return p;
        }

        ChunkData PayloadToBlock(byte[] payload)
        {
            var bit = new BitListEnumerator(payload.ToList());
            bit.MoveNextToShort();
            var x = bit.MoveNextToInt();
            var y = bit.MoveNextToInt();
            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];
            for (int i = 0; i < ChunkResponseConst.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkResponseConst.ChunkSize; j++)
                {
                    //空気ブロックか否か
                    if (bit.MoveNextToBit())
                    {
                        //ブロックIDの取得
                        //intか否か
                        if (bit.MoveNextToBit())
                        {
                            bit.MoveNextToBit();
                            blocks[i, j] = bit.MoveNextToInt();
                        }
                        else
                        {
                            //shortかbyteか
                            if (bit.MoveNextToBit())
                            {
                                blocks[i, j] = bit.MoveNextToShort();
                            }
                            else
                            {
                                blocks[i, j] = bit.MoveNextToByte();
                            }
                        }
                    }
                    else
                    {
                        //空気ブロック
                        blocks[i, j] = BlockConst.EmptyBlockId;
                    }
                }
            }

            return new ChunkData(blocks, new Coordinate(x, y));
        }

        private class ChunkData
        {
            public readonly int[,] Blocks;
            public readonly Coordinate Coordinate;

            public ChunkData(int[,] blocks, Coordinate coordinate)
            {
                this.Blocks = blocks;
                Coordinate = coordinate;
            }
        }
    }
}