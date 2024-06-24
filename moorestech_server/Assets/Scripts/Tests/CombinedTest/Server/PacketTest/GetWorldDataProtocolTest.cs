using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetWorldDataProtocolTest
    {
        public const int Block_1x4_Id = 9; // 1x4サイズのブロックのID
        
        //ランダムにブロックを設置するテスト
        [Test]
        public void RandomPlaceBlockToWorldDataResponseTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            var random = new Random(13944156);
            //ブロックの設置
            for (var i = 0; i < 1000; i++)
            {
                var blockDirection = (BlockDirection)random.Next(0, 4);
                var pos = new Vector3Int(random.Next(-40, 40), random.Next(-40, 40));
                
                var posInfo = new BlockPositionInfo(pos, blockDirection, Vector3Int.one);
                
                IBlock b = null;
                if (random.Next(0, 3) == 1)
                    b = blockFactory.Create(random.Next(short.MaxValue, int.MaxValue), BlockInstanceId.Create(), posInfo);
                else
                    b = blockFactory.Create(random.Next(1, 500), BlockInstanceId.Create(), posInfo);
                
                
                worldBlock.TryAddBlock(b);
            }
            
            var requestBytes = MessagePackSerializer.Serialize(new RequestWorldDataMessagePack());
            List<byte> responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseWorld = MessagePackSerializer.Deserialize<ResponseWorldDataMessagePack>(responseBytes.ToArray());
            
            //検証
            for (var i = 0; i < responseWorld.Blocks.Length; i++)
            {
                var block = responseWorld.Blocks[i];
                var pos = block.BlockPos;
                
                var id = worldBlock.GetOriginPosBlock(pos)?.Block.BlockId ?? BlockConst.EmptyBlockId;
                Assert.AreEqual(id, block.BlockId);
                
                var direction = worldBlock.GetOriginPosBlock(pos)?.BlockPositionInfo.BlockDirection ?? BlockDirection.North;
                Assert.AreEqual(direction, block.BlockDirection);
            }
        }
        
        //マルチブロックを設置するテスト
        [Test]
        public void PlaceBlockToWorldDataTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //ブロックの設置
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var b = blockFactory.Create(Block_1x4_Id, new BlockInstanceId(1), posInfo);
            worldBlock.TryAddBlock(b);
            
            var requestBytes = MessagePackSerializer.Serialize(new RequestWorldDataMessagePack());
            List<byte> responseBytes = packetResponse.GetPacketResponse(requestBytes.ToList())[0];
            var responseWorld = MessagePackSerializer.Deserialize<ResponseWorldDataMessagePack>(responseBytes.ToArray());
            
            //ブロックが設置されていることを確認する
            Assert.AreEqual(Block_1x4_Id, responseWorld.Blocks[0].BlockId);
        }
    }
}