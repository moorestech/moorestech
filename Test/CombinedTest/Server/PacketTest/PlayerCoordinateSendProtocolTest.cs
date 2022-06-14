using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Const;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;
using EntityId = Game.World.Interface.Util.EntityId;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlayerCoordinateSendProtocolTest
    {
        [Test, Order(1)]
        public void SimpleChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            //1回のレスポンスのテスト
            packetResponse.GetPacketResponse(GetHandshakePacket(10));
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
                Assert.True(ans.Contains(r.Coordinate));
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
            packetResponse.GetPacketResponse(GetHandshakePacket(15));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(15, 0, 0))
                .Select(PayloadToBlock).ToList();

            Assert.AreEqual(25, response.Count());
        }


        //ブロックを設置するテスト
        [Test, Order(2)]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            var random = new Random(13944156);
            //ブロックの設置
            var b = blockFactory.Create(5,1);
            worldBlock.AddBlock(b, 0, 0, BlockDirection.North);



            packetResponse.GetPacketResponse(GetHandshakePacket(20));
            
            var bytes = packetResponse.GetPacketResponse(PlayerCoordinatePayload(20, 0, 0));
            var response = bytes.Select(PayloadToBlock).ToList();

            
            
            
            
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
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        Assert.AreEqual(worldBlock.GetBlock(c.X + i, c.Y + j).BlockId
                            , r.Blocks[i, j]);
                    }
                }
            }
        }

        //ランダムにブロックを設置するテスト
        [Test, Order(3)]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            var random = new Random(13944156);
            //ブロックの設置
            for (int i = 0; i < 1000; i++)
            {
                IBlock b = null;
                if (random.Next(0, 3) == 1)
                {
                    b = blockFactory.Create(random.Next(short.MaxValue, int.MaxValue),EntityId.NewEntityId());
                }
                else
                {
                    b = blockFactory.Create(random.Next(1, 500),EntityId.NewEntityId());
                }
                
                
                var blockDirection = (BlockDirection)random.Next(0, 4);
                worldBlock.AddBlock(b, random.Next(-40, 40), random.Next(-40, 40), blockDirection);
            }
            
            
            packetResponse.GetPacketResponse(GetHandshakePacket(25));


            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(25, 0, 0))
                .Select(PayloadToBlock).ToList();
            
            
            
            
            //検証
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
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (int i = 0; i < r.Blocks.GetLength(0); i++)
                {
                    for (int j = 0; j < r.Blocks.GetLength(1); j++)
                    {
                        var id = worldBlock.GetBlock(c.X + i, c.Y + j).BlockId;
                        Assert.AreEqual(id, r.Blocks[i, j]);
                        
                        var direction = worldBlock.GetBlockDirection(c.X + i, c.Y + j);
                        Assert.AreEqual(direction, r.BlockDirections[i, j]);
                    }
                }
            }
        }

        //マップタイルの返信が正しいかチェックする
        [Test, Order(4)]
        public void TileMapResponseTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();
            var worldMapTile = serviceProvider.GetService<WorldMapTile>();
            
            var veinCoordinate = new Coordinate(0, 0);
            
            //10000*10000 ブロックの中から鉱石があるチャンクを探す
            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 1000; j++)
                {
                    var id = veinGenerator.GetOreId(i, j);
                    if (OreConst.NoneOreId == id) continue;
                    veinCoordinate = new Coordinate(i, j);
                }
            }
            
            //鉱石がある座標のチャンクを取得
            packetResponse.GetPacketResponse(GetHandshakePacket(25));
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(25, veinCoordinate.X, veinCoordinate.Y))
                .Select(PayloadToBlock).ToList();
            
            
            //正しく鉱石IDが帰ってきているかチェックする
            foreach (var r in response)
            {
                var x = r.Coordinate.X;
                var y = r.Coordinate.Y;
                for (int i = 0; i < ChunkResponseConst.ChunkSize; i++)
                {
                    for (int j = 0; j < ChunkResponseConst.ChunkSize; j++)
                    {
                        //マップタイルと実際の返信を検証する
                        Assert.AreEqual(
                            worldMapTile.GetMapTile(i + x,j + y), 
                            r.MapTiles[i, j]);
                    }
                }
            }
        }

        List<byte> PlayerCoordinatePayload(int playerId, float x, float y)
        {
            return MessagePackSerializer.Serialize(new PlayerCoordinateSendProtocolMessagePack(playerId,x,y)).ToList();
        }


        private List<byte> GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId,"test player name")).ToList();
        }

        ChunkData PayloadToBlock(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<ChunkDataResponseMessagePack>(payload.ToArray());
            

            return new ChunkData(new Coordinate(data.ChunkX, data.ChunkY),data.BlockIds,data.MapTileIds,data.BlockDirect);
        }

        private class ChunkData
        {
            public readonly int[,] Blocks;
            public readonly BlockDirection[,] BlockDirections;
            public readonly int[,] MapTiles;
            public readonly Coordinate Coordinate;

            public ChunkData(Coordinate coordinate, int[,] blocks,int[,] mapTiles, int[,] blockDirections)
            {
                Blocks = blocks;
                Coordinate = coordinate;
                MapTiles = mapTiles;
                BlockDirections = new BlockDirection[ChunkResponseConst.ChunkSize,ChunkResponseConst.ChunkSize];

                for (int i = 0; i < ChunkResponseConst.ChunkSize; i++)
                {
                    for (int j = 0; j < ChunkResponseConst.ChunkSize; j++)
                    {
                        BlockDirections[i,j] = (BlockDirection)blockDirections[i,j];
                    }
                }
            }
        }
    }
}