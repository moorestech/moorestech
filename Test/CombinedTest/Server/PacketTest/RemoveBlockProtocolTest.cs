using System.Collections.Generic;
using Core.Block.BlockFactory;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Event.EventReceive;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        
        [Test]
        public void RemoveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var BlockFactory = serviceProvider.GetService<BlockFactory>();

            var Block = BlockFactory.Create(1, 0);
            
            //削除するためのブロックの生成
            worldBlock.AddBlock(Block, 0, 0, BlockDirection.North);
            
            Assert.AreEqual(0,worldBlock.GetBlock(0,0).GetIntId());
            
            //プロトコルを使ってブロックを削除
            packet.GetPacketResponse(RemoveBlock(0, 0, 0));
            
            Assert.False(worldBlock.Exists(0,0));

        }

        List<byte> RemoveBlock(int x, int y,int Playerid)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ToByteList.Convert((short) 10));
            bytes.AddRange(ToByteList.Convert(x));
            bytes.AddRange(ToByteList.Convert(y));
            bytes.AddRange(ToByteList.Convert(Playerid));

            return bytes;
        }
        
    }
}