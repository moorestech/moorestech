using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.Blocks;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Event;
using Server.Protocol.PacketResponse;
using Server.StartServerSystem;
using Server.Util;
using Test.Module.TestConfig;
using Test.Module.TestMod;
using World.Event;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class BlockPlaceEventPacketTest
    {
        //ブロックを設置しなかった時何も返ってこないテスト
        [Test]
        public void DontBlockPlaceTest()
        {
            var blockPlace = new BlockPlaceEvent();

            var eventProtocol = new EventProtocolProvider();
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            throw new NotImplementedException();
            Assert.AreEqual(response.Count, 0);
        }

        //ブロックを0個以上設置した時にブロック設置イベントが返ってくるテスト
        [Test]
        public void BlockPlaceEvent()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            
            

            //イベントキューにIDを登録する
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0, response.Count);

            var random = new Random(1410);
            for (int i = 0; i < 100; i++)
            {
                //ランダムな位置にブロックを設置する
                Console.WriteLine(i);
                var blocks = new List<TestBlockData>();
                var cnt = random.Next(0, 20);
                for (int j = 0; j < cnt; j++)
                {
                    var x = random.Next(-10000, 10000);
                    var y = random.Next(-10000, 10000);
                    var blockId = random.Next(1, 1000);
                    var direction = random.Next(0, 4);
                    
                    //設置したブロックを保持する
                    blocks.Add(new TestBlockData(x, y, blockId,direction));
                    //ブロックの設置
                    worldBlockDataStore.AddBlock(new VanillaBlock(blockId,random.Next(1, 1000000),1), x, y,(BlockDirection)direction);
                }
                
                
                
                //イベントパケットをリクエストする
                response = packetResponse.GetPacketResponse(EventRequestData(0));
                Assert.AreEqual(cnt, response.Count);
                
                
                
                
                //返ってきたイベントパケットと設置したブロックを照合し、あったら削除する
                foreach (var r in response)
                {
                    var b = AnalysisResponsePacket(r);
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        if (b.Equals(blocks[j]))
                        {
                            blocks.RemoveAt(j);
                        }
                    }
                }
                //設置したブロックリストが残ってなければすべてのイベントが返ってきた事がわかる
                Assert.AreEqual(0, blocks.Count);
                
                

                //イベントのリクエストを送ったので次は何も返ってこないテスト
                response = packetResponse.GetPacketResponse(EventRequestData(0));
                Assert.AreEqual(0, response.Count);
            }
        }

        TestBlockData AnalysisResponsePacket(List<byte> payload)
        {
            var b = new ByteListEnumerator(payload.ToList());
            b.MoveNextToGetShort();
            var eventId = b.MoveNextToGetShort();
            Assert.AreEqual(0, eventId);
            var x = b.MoveNextToGetInt();
            var y = b.MoveNextToGetInt();
            var id = b.MoveNextToGetInt();
            var direction = b.MoveNextToGetByte();
            
            return new TestBlockData(x, y, id,direction);
        }

        List<byte> EventRequestData(int plyaerID)
        {   
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID)).ToList();;
        }

        class TestBlockData
        {
            public readonly int X;
            public readonly int Y;
            public readonly int id;
            public readonly BlockDirection BlockDirection;

            public TestBlockData(int x, int y, int id, int blockDirectionNum)
            {
                X = x;
                Y = y;
                this.id = id;
                BlockDirection = (BlockDirection) blockDirectionNum;
            }

            public override bool Equals(object? obj)
            {
                var b = obj as TestBlockData;
                return
                    b.id == id &&
                    b.X == X &&
                    b.Y == Y &&
                    b.BlockDirection == BlockDirection;
            }
        }
    }
}