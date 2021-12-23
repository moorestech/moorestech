using System;
using System.Collections.Generic;
using System.Linq;
using Game.World.Interface;
using Game.World.Interface.Event;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Event;
using Server.PacketHandle;
using Server.Protocol;
using Server.Util;
using World;
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
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(response.Count,0);
        }
        
        //ブロックを0個以上設置した時にブロック設置イベントが返ってくるテスト
        [Test]
        public void BlockPlaceEvent()
        {
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            //イベントキューにIDを登録する
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0,response.Count);
            
            var random = new Random(1410);
            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine(i);
                var blocks = new List<Block>();
                var cnt = random.Next(0, 20);
                for (int j = 0; j < cnt; j++)
                {
                    var x = random.Next(-10000, 10000);
                    var y = random.Next(-10000, 10000);
                    var id = random.Next(1, 1000);
                    blocks.Add(new Block(x,y,id));
                    BlockPlace(x, y, id,packetResponse);
                }
                
                response = packetResponse.GetPacketResponse(EventRequestData(0));
                Assert.AreEqual(cnt,response.Count);
                //帰ってきたイベントが設置したブロックであることを確認
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
                Assert.AreEqual(0,blocks.Count);
                
                //パケットを送ったので次は何も返ってこない
                response = packetResponse.GetPacketResponse(EventRequestData(0));
                Assert.AreEqual(0,response.Count);
            }
        }
        
        Block AnalysisResponsePacket(byte[] payload)
        {
            var b = new ByteArrayEnumerator(payload.ToList());
            b.MoveNextToGetShort();
            var eventId = b.MoveNextToGetShort();
            Assert.AreEqual(0,eventId);
            var x = b.MoveNextToGetInt();
            var y = b.MoveNextToGetInt();
            var id = b.MoveNextToGetInt();
            return new Block(x,y,id);
        }
        void BlockPlace(int x,int y,int id,PacketResponseCreator packetResponseCreator)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ByteListConverter.ToByteArray((short)1));
            bytes.AddRange(ByteListConverter.ToByteArray(id));
            bytes.AddRange(ByteListConverter.ToByteArray((short)0));
            bytes.AddRange(ByteListConverter.ToByteArray(x));
            bytes.AddRange(ByteListConverter.ToByteArray(y));
            bytes.AddRange(ByteListConverter.ToByteArray(0));
            bytes.AddRange(ByteListConverter.ToByteArray(0));
            packetResponseCreator.GetPacketResponse(bytes);
        }

        List<byte> EventRequestData(int plyaerID)
        {
            var payload = new List<byte>();
            payload.AddRange(ByteListConverter.ToByteArray((short)4));
            payload.AddRange(ByteListConverter.ToByteArray(plyaerID));
            return payload;
        }

        class Block
        {
            public readonly int X;
            public readonly int Y;
            public readonly int id;

            public Block(int x, int y, int id)
            {
                X = x;
                Y = y;
                this.id = id;
            }

            public override bool Equals(object? obj)
            {
                var b = obj as Block;
                return 
                    b.id == id &&
                    b.X == X &&
                    b.Y == Y;
            }
        }
    }
}