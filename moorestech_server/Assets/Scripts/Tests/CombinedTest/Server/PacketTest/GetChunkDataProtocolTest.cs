using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Block.Interface;
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
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            var random = new Random(13944156);
            //ブロックの設置
            for (var i = 0; i < 1000; i++)
            {
                var blockDirection = (BlockDirection)random.Next(0, 4);
                var pos = new Vector3Int(random.Next(-40, 40), random.Next(-40, 40));
                
                var posInfo = new BlockPositionInfo(pos, blockDirection, Vector3Int.one);
                
                IBlock b = null;
                if (random.Next(0, 3) == 1)
                    b = blockFactory.Create(random.Next(short.MaxValue, int.MaxValue), CreateBlockEntityId.Create(),posInfo);
                else
                    b = blockFactory.Create(random.Next(1, 500), CreateBlockEntityId.Create(),posInfo);


                worldBlock.AddBlock(b);
            }


            var requestChunks = new List<Vector2IntMessagePack>()
            {
                new (new(0, 0)),
                new (new(0, ChunkResponseConst.ChunkSize)),
                new (new(ChunkResponseConst.ChunkSize, 0)),
                new (new(ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize))
            };
            var requestBytes = MessagePackSerializer.Serialize(new RequestChunkDataMessagePack(requestChunks));
            var responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseChunks = MessagePackSerializer.Deserialize<ResponseChunkDataMessagePack>(responseBytes.ToArray()).ChunkData;

            //検証
            foreach (var r in responseChunks)
            {
                //座標の確認
                var c = r.ChunkPos;
                for (int i = 0; i < r.Blocks.Length; i++)
                {
                    var block = r.Blocks[i];
                    var pos = block.BlockPos;
                    
                    var id = worldBlock.GetOriginPosBlock(pos)?.Block.BlockId ?? BlockConst.EmptyBlockId;
                    Assert.AreEqual(id, block.BlockId);

                    var direction = worldBlock.GetOriginPosBlock(pos)?.BlockPositionInfo.BlockDirection ?? BlockDirection.North;
                    Assert.AreEqual(direction, block.BlockDirection);
                }
            }
        }
        
        //マルチブロックを設置するテスト
        [Test]
        public void PlaceBlockToChunkResponseTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            //ブロックの設置
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var b = blockFactory.Create(Block_1x4_Id, 1,posInfo);
            worldBlock.AddBlock(b);

            var requestChunks = new List<Vector2IntMessagePack>() { new (new (0, 0)) };
            var requestBytes = MessagePackSerializer.Serialize(new RequestChunkDataMessagePack(requestChunks));
            var responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseChunks = MessagePackSerializer.Deserialize<ResponseChunkDataMessagePack>(responseBytes.ToArray()).ChunkData;
            
            var chunkData = responseChunks[0];
            
            //ブロックが設置されていることを確認する
            Assert.AreEqual(new Vector2Int(0, 0), (Vector2Int)chunkData.ChunkPos);
            Assert.AreEqual(Block_1x4_Id, chunkData.Blocks[0].BlockId);
            
        }
    }
}