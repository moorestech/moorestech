using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Ore;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlayerCoordinateSendProtocolTest
    {
        public const int Block_1x4_Id = 9;

        [Test]
        public void SimpleChunkResponseTest()
        {
            var (packetResponse, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            //1回のレスポンスのテスト
            packetResponse.GetPacketResponse(GetHandshakePacket(10));
            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();

            Assert.AreEqual(25, response.Count());
            var ans = new List<Vector2Int>();
            for (var i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            for (var j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                ans.Add(new Vector2Int(i, j));

            foreach (var r in response)
            {
                //座標の確認
                Assert.True(ans.Contains(r.ChunkOrigin));
                //ブロックの確認
                for (var i = 0; i < r.Blocks.GetLength(0); i++)
                for (var j = 0; j < r.Blocks.GetLength(1); j++)
                    Assert.AreEqual(BlockConst.EmptyBlockId, r.Blocks[i, j]);
            }

            //2回目は何も返ってこないテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();
            Assert.AreEqual(response.Count, 0);


            //場所をずらしたら返ってくるテスト
            packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 0, 0));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(10, 25, 25))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();
            Assert.AreEqual(response.Count, 9);

            //他の名前は普通に取得できるテスト
            packetResponse.GetPacketResponse(GetHandshakePacket(15));
            response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(15, 0, 0))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();

            Assert.AreEqual(25, response.Count());
        }

        //ブロックを設置するテスト
        [Test]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            //ブロックの設置
            var b = blockFactory.Create(Block_1x4_Id, 1);
            worldBlock.AddBlock(b, 0, 0, BlockDirection.North);

            packetResponse.GetPacketResponse(GetHandshakePacket(20));

            var bytes = packetResponse.GetPacketResponse(PlayerCoordinatePayload(20, 0, 0));
            var response = bytes.Select(PayloadToBlock).Where(p => p is not null).ToList();


            Assert.AreEqual(25, response.Count());
            var ans = new List<Vector2Int>();
            for (var i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            for (var j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                ans.Add(new Vector2Int(i, j));

            foreach (var r in response)
            {
                //座標の確認
                var c = r.ChunkOrigin;
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (var i = 0; i < r.Blocks.GetLength(0); i++)
                for (var j = 0; j < r.Blocks.GetLength(1); j++)
                    if (i == 0 && j == 0 && c == new Vector2Int(0, 0))
                        Assert.AreEqual(Block_1x4_Id, r.Blocks[i, j]);
                    else
                        Assert.AreEqual(BlockConst.EmptyBlockId, r.Blocks[i, j]);
            }
        }

        //ランダムにブロックを設置するテスト
        [Test]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var random = new Random(13944156);
            //ブロックの設置
            for (var i = 0; i < 1000; i++)
            {
                IBlock b = null;
                if (random.Next(0, 3) == 1)
                    b = blockFactory.Create(random.Next(short.MaxValue, int.MaxValue), CreateBlockEntityId.Create());
                else
                    b = blockFactory.Create(random.Next(1, 500), CreateBlockEntityId.Create());


                var blockDirection = (BlockDirection)random.Next(0, 4);
                worldBlock.AddBlock(b, random.Next(-40, 40), random.Next(-40, 40), blockDirection);
            }


            packetResponse.GetPacketResponse(GetHandshakePacket(25));


            var response = packetResponse.GetPacketResponse(PlayerCoordinatePayload(25, 0, 0))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();


            //検証
            Assert.AreEqual(25, response.Count());
            var ans = new List<Vector2Int>();
            for (var i = -40; i <= 40; i += ChunkResponseConst.ChunkSize)
            for (var j = -40; j <= 40; j += ChunkResponseConst.ChunkSize)
                ans.Add(new Vector2Int(i, j));

            foreach (var r in response)
            {
                //座標の確認
                var c = r.ChunkOrigin;
                Assert.True(ans.Contains(c));
                //ブロックの確認
                for (var i = 0; i < r.Blocks.GetLength(0); i++)
                for (var j = 0; j < r.Blocks.GetLength(1); j++)
                {
                    var id = worldBlock.GetOriginPosBlock(c.x + i, c.y + j)?.Block.BlockId ?? BlockConst.EmptyBlockId;
                    Assert.AreEqual(id, r.Blocks[i, j]);

                    var direction = worldBlock.GetOriginPosBlock(c.x + i, c.y + j)?.BlockDirection ??
                                    BlockDirection.North;
                    Assert.AreEqual(direction, r.BlockDirections[i, j]);
                }
            }
        }

        //マップタイルの返信が正しいかチェックする
        [Test]
        public void TileMapResponseTest()
        {
            var (packetResponse, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();
            var worldMapTile = serviceProvider.GetService<WorldMapTile>();

            var veinCoordinate = new Vector2Int(0, 0);

            //10000*10000 ブロックの中から鉱石があるチャンクを探す
            for (var i = 0; i < 1000; i++)
            for (var j = 0; j < 1000; j++)
            {
                var id = veinGenerator.GetOreId(i, j);
                if (OreConst.NoneOreId == id) continue;
                veinCoordinate = new Vector2Int(i, j);
            }

            //鉱石がある座標のチャンクを取得
            packetResponse.GetPacketResponse(GetHandshakePacket(25));
            var response = packetResponse
                .GetPacketResponse(PlayerCoordinatePayload(25, veinCoordinate.x, veinCoordinate.y))
                .Select(PayloadToBlock).Where(p => p is not null).ToList();


            //正しく鉱石IDが帰ってきているかチェックする
            foreach (var r in response)
            {
                var x = r.ChunkOrigin.x;
                var y = r.ChunkOrigin.y;
                for (var i = 0; i < ChunkResponseConst.ChunkSize; i++)
                for (var j = 0; j < ChunkResponseConst.ChunkSize; j++)
                    //マップタイルと実際の返信を検証する
                    Assert.AreEqual(
                        worldMapTile.GetMapTile(i + x, j + y),
                        r.MapTiles[i, j]);
            }
        }

        private List<byte> PlayerCoordinatePayload(int playerId, float x, float y)
        {
            return MessagePackSerializer.Serialize(new PlayerCoordinateSendProtocolMessagePack(playerId, x, y))
                .ToList();
        }


        private List<byte> GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId, "test player name")).ToList();
        }

        private ChunkData PayloadToBlock(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<ChunkDataResponseMessagePack>(payload.ToArray());
            //エンティティタグの時はnullを返す
            if (data.Tag == PlayerCoordinateSendProtocol.EntityDataTag) return null;


            return new ChunkData(new Vector2Int(data.ChunkX, data.ChunkY), data.BlockIds, data.MapTileIds,
                data.BlockDirect);
        }

        private class ChunkData
        {
            public readonly BlockDirection[,] BlockDirections;
            public readonly int[,] Blocks;
            public readonly Vector2Int ChunkOrigin;
            public readonly int[,] MapTiles;

            public ChunkData(Vector2Int chunkOrigin, int[,] blocks, int[,] mapTiles, int[,] blockDirections)
            {
                Blocks = blocks;
                ChunkOrigin = chunkOrigin;
                MapTiles = mapTiles;
                BlockDirections = new BlockDirection[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

                for (var i = 0; i < ChunkResponseConst.ChunkSize; i++)
                for (var j = 0; j < ChunkResponseConst.ChunkSize; j++)
                    BlockDirections[i, j] = (BlockDirection)blockDirections[i, j];
            }
        }
    }
}