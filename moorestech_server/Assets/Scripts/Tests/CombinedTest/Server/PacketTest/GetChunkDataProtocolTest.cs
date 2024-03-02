using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Const;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetChunkDataProtocolTest
    {
        public const int Block_1x4_Id = 9; // 1x4サイズのブロックのID

        //ランダムにブロックを設置するテスト
        [Test]
        public void RandomPlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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


            var requestChunks = new List<Vector2IntMessagePack>()
            {
                new (0, 0),
                new (0, ChunkResponseConst.ChunkSize),
                new (ChunkResponseConst.ChunkSize, 0),
                new (ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize)
            };
            var requestBytes = MessagePackSerializer.Serialize(new RequestChunkDataMessagePack(requestChunks));
            var responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseChunks = MessagePackSerializer.Deserialize<ResponseChunkDataMessagePack>(responseBytes.ToArray()).ChunkData;

            //検証
            foreach (var r in responseChunks)
            {
                //座標の確認
                var c = r.ChunkPos;
                //ブロックの確認
                for (var i = 0; i < r.BlockIds.GetLength(0); i++)
                for (var j = 0; j < r.BlockIds.GetLength(1); j++)
                {
                    var id = worldBlock.GetOriginPosBlock(c.X + i, c.Y + j)?.Block.BlockId ?? BlockConst.EmptyBlockId;
                    Assert.AreEqual(id, r.BlockIds[i, j]);

                    var direction = worldBlock.GetOriginPosBlock(c.X + i, c.Y + j)?.BlockDirection ?? BlockDirection.North;
                    Assert.AreEqual(direction, (BlockDirection)r.BlockDirections[i, j]);
                }
            }
        }
        
        //マルチブロックを設置するテスト
        [Test]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            //ブロックの設置
            var b = blockFactory.Create(Block_1x4_Id, 1);
            worldBlock.AddBlock(b, 0, 0, BlockDirection.North);

            var requestChunks = new List<Vector2IntMessagePack>() { new (0, 0) };
            var requestBytes = MessagePackSerializer.Serialize(new RequestChunkDataMessagePack(requestChunks));
            var responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseChunks = MessagePackSerializer.Deserialize<ResponseChunkDataMessagePack>(responseBytes.ToArray()).ChunkData;
            
            var chunkData = responseChunks[0];
            
            //座標の確認
            var c = chunkData.ChunkPos.Vector2Int;
            Assert.AreEqual(new Vector2Int(0, 0), c);
            //ブロックの確認
            for (var i = 0; i < chunkData.BlockIds.GetLength(0); i++)
            for (var j = 0; j < chunkData.BlockIds.GetLength(1); j++)
                if (i == 0 && j == 0)
                    Assert.AreEqual(Block_1x4_Id, chunkData.BlockIds[i, j]);
                else
                {
                    Debug.Log(chunkData.BlockIds[i, j] + " " + i + " " + j);
                    Assert.AreEqual(BlockConst.EmptyBlockId, chunkData.BlockIds[i, j]);
                }
        }
    }
}