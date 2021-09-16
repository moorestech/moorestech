using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core;
using Core.Block;
using Core.Block.Machine.util;
using Core.Util;
using industrialization.OverallManagement.DataStore;
using industrialization.OverallManagement.Util;
using industrialization.Server.PacketHandle;
using industrialization.Server.PacketHandle.PacketResponse;
using industrialization.Server.Util;
using NUnit.Framework;
using Server.Const;

namespace industrialization_test.CombinedTest.Server.PacketTest
{
    public class PlayerCoordinateSendProtocolTest
    {
        [Test, Order(1)]
        public void SimpleChunkResponseTest()
        {
            
            //1回のレスポンスのテスト
            var response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("a", 0, 0))
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
                        Assert.AreEqual(BlockConst.NullBlockId,r.Blocks[i,j]);
                    }
                }
                
            }
            
            //2回目は何も返ってこないテスト
            PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("a", 0, 0));
            response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("a", 0, 0))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count,0);
            
            
            //場所をずらしたら返ってくるテスト
            PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("a", 0, 0));
            response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("a", 25, 25))
                .Select(PayloadToBlock).ToList();
            Assert.AreEqual(response.Count,9);
            
            //他の名前は普通に取得できるテスト
            response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("aa", 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25,response.Count());
        }
        
        
        //ブロックを設置するテスト
        [Test, Order(2)]
        public void PlaceBlockToChunkResponseTest()
        {
            WorldBlockDatastore.ClearData();
            
            var random = new Random(13944156);
            //ブロックの設置
            var b = NormalMachineFactory.Create(5, IntId.NewIntId(), new NullIBlockInventory());
            WorldBlockDatastore.AddBlock(b, 0, 0);
            
            var response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("b", 0, 0))
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
                        Assert.AreEqual(WorldBlockDatastore.GetBlock(c.x + i, c.y + j).GetBlockId()
                            ,r.Blocks[i,j]);
                    }
                }
                
            }
        }
        
        //ランダムにブロックを設置するテスト
        [Test, Order(3)]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            WorldBlockDatastore.ClearData();
            
            var random = new Random(13944156);
            //ブロックの設置
            for (int i = 0; i < 1000; i++)
            {
                IBlock b = null;
                if (random.Next(0, 3) == 1)
                {
                    b = NormalMachineFactory.Create(random.Next(short.MaxValue, int.MaxValue), IntId.NewIntId(), new NullIBlockInventory());
                }
                else
                {
                    b = NormalMachineFactory.Create(random.Next(0, 500), IntId.NewIntId(), new NullIBlockInventory());
                }
                WorldBlockDatastore.AddBlock(b, random.Next(-300, 300), random.Next(-300, 300));
            }
            
            var response = PacketResponseCreator.GetPacketResponse(PlayerCoordinatePayload("c", 0, 0))
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
                        Assert.AreEqual(WorldBlockDatastore.GetBlock(c.x + i, c.y + j).GetBlockId()
                            ,r.Blocks[i,j]);
                    }
                }
                
            }
        }

        byte[] PlayerCoordinatePayload(string playerName, float x, float y)
        {
            var p = new List<byte>();
            p.AddRange(ByteArrayConverter.ToByteArray((short)2));
            p.AddRange(ByteArrayConverter.ToByteArray(x));
            p.AddRange(ByteArrayConverter.ToByteArray(y));
            p.AddRange(ByteArrayConverter.ToByteArray((short)Encoding.UTF8.GetByteCount(playerName)));
            p.AddRange(ByteArrayConverter.ToByteArray(playerName));
            return p.ToArray();
        }

        ChunkData PayloadToBlock(byte[] payload)
        {
            var bit= new BitArrayEnumerator(payload);
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
                        blocks[i, j] = BlockConst.NullBlockId;
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