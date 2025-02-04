using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.RequestWorldDataProtocol;
using Random = System.Random;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetWorldDataProtocolTest
    {
        public static readonly BlockId Block_1x4_Id = new(9); // 1x4サイズのブロックのID
        
        //ランダムにブロックを設置するテスト
        [Test]
        public void RandomPlaceBlockToWorldDataResponseTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var random = new Random(13944156);
            //ブロックの設置
            for (var i = 0; i < 1000; i++)
            {
                var blockDirection = (BlockDirection)random.Next(0, 4);
                var pos = new Vector3Int(random.Next(-40, 40), random.Next(-40, 40));
                
                var blockId = random.Next(1, 20);
                worldBlockDatastore.TryAddBlock((BlockId)blockId, pos, blockDirection, out _);
            }
            
            var requestBytes = MessagePackSerializer.Serialize(new RequestWorldDataMessagePack());
            List<byte> responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseWorld = MessagePackSerializer.Deserialize<ResponseWorldDataMessagePack>(responseBytes.ToArray());
            
            //検証
            for (var i = 0; i < responseWorld.Blocks.Length; i++)
            {
                var block = responseWorld.Blocks[i];
                var pos = block.BlockPos;
                
                var id = worldBlockDatastore.GetOriginPosBlock(pos)?.Block.BlockId ?? new BlockId(0);
                Assert.AreEqual(id, block.BlockId);
                
                var direction = worldBlockDatastore.GetOriginPosBlock(pos)?.BlockPositionInfo.BlockDirection ?? BlockDirection.North;
                Assert.AreEqual(direction, block.BlockDirection);
            }
        }
        
        //マルチブロックを設置するテスト
        [Test]
        public void PlaceBlockToWorldDataTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = ServerContext.WorldBlockDatastore;
            
            //ブロックの設置
            worldBlock.TryAddBlock(Block_1x4_Id, Vector3Int.zero, BlockDirection.North, out _);
            
            var requestBytes = MessagePackSerializer.Serialize(new RequestWorldDataMessagePack());
            List<byte> responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseWorld = MessagePackSerializer.Deserialize<ResponseWorldDataMessagePack>(responseBytes.ToArray());
            
            //ブロックが設置されていることを確認する
            Assert.AreEqual(Block_1x4_Id, responseWorld.Blocks[0].BlockId);
        }
    }
}