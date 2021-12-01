using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Block;
using Core.Block.Machine;
using Core.Block.Machine.util;
using Core.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Const;
using Server.Event;
using Server.PacketHandle;
using Server.Util;
using World;
using World.Event;
using World.Util;
using IntId = World.IntId;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlayerCoordinateSendProtocolTest
    {
        [Test, Order(1)]
        public void SimpleChunkResponseTest()
        {
            
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            //1回のレスポンスのテスト
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25,response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i+=ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j+=ChunkResponseConst.ChunkSize)
                {
                    ans.Add(CoordinateCreator.New(i, j));
                }
            }
            foreach (var r in response)
            {
                //座標の確認
                Assert.True(ans.Contains(r.Coordinate));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(BlockConst.BlockConst.NullBlockId,r.Blocks[i,j]);
                    }
                }
                
            }
            
            //2回目は何も返ってこないテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count,0);
            
            
            //場所をずらしたら返ってくるテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 25, 25))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count,9);
            
            //他の名前は普通に取得できるテスト
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(15, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25,response.Count());
        }
        
        
        //ブロックを設置するテスト
        [Test, Order(2)]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<WorldBlockDatastore>();
            
            var random = new Random(13944156);
            //ブロックの設置
            var b = NormalMachineFactory.Create(5, IntId.NewIntId(), new NullIBlockInventory());
            worldBlock.AddBlock(b, 0, 0,b);
            
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(20, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25,response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i+=ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j+=ChunkResponseConst.ChunkSize)
                {
                    ans.Add(CoordinateCreator.New(i, j));
                }
            }
            foreach (var r in response)
            {
                //座標の確認
                var c = r.Coordinate;
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(worldBlock.GetBlock(c.x + i, c.y + j).GetBlockId()
                            ,r.Blocks[i,j]);
                    }
                }
                
            }
        }
        
        //ランダムにブロックを設置するテスト
        [Test, Order(3)]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<WorldBlockDatastore>();

            var random = new Random(13944156);
            //ブロックの設置
            for (int i = 0; i < 1000; i++)
            {
                NormalMachine b = null;
                if (random.Next(0, 3) == 1)
                {
                    b = NormalMachineFactory.Create(random.Next(short.MaxValue, int.MaxValue), IntId.NewIntId(), new NullIBlockInventory());
                }
                else
                {
                    b = NormalMachineFactory.Create(random.Next(0, 500), IntId.NewIntId(), new NullIBlockInventory());
                }
                worldBlock.AddBlock(b, random.Next(-300, 300), random.Next(-300, 300),b);
            }
            
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(25, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25,response.Count());
            var ans = new List<Coordinate>();
            for (int i = -40; i <= 40; i+=ChunkResponseConst.ChunkSize)
            {
                for (int j = -40; j <= 40; j+=ChunkResponseConst.ChunkSize)
                {
                    ans.Add(CoordinateCreator.New(i, j));
                }
            }
            foreach (var r in response)
            {
                //座標の確認
                var c = r.Coordinate;
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(worldBlock.GetBlock(c.x + i, c.y + j).GetBlockId()
                            ,r.Blocks[i,j]);
                    }
                }
                
            }
        }

        List<byte> PlayerCoordinatePayload(int playerId, float x, float y)
        {
            var p = new List<byte>();
            p.AddRange(ByteListConverter.ToByteArray((short)2));
            p.AddRange(ByteListConverter.ToByteArray(x));
            p.AddRange(ByteListConverter.ToByteArray(y));
            p.AddRange(ByteListConverter.ToByteArray(playerId));
            return p;
        }

        ChunkData PayloadToBlock(byte[] payload)
        {
            var bit= new BitListEnumerator(payload.ToList());
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
                        blocks[i, j] = BlockConst.BlockConst.NullBlockId;
                    }
                }
            }

            return new ChunkData(blocks,CoordinateCreator.New(x,y));
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