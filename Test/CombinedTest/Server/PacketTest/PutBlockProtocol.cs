using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server;
using Server.Event;
using Server.PacketHandle;
using Server.Util;
using World;
using World.Event;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PutBlockProtocol
    {
        [Test]
        public void SimpleBlockPlaceTest()
        {
            var (packetResponse,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlock = serviceProvider.GetService<WorldBlockDatastore>();
            
            packetResponse.GetPacketResponse(BlockPlace(1, 0, 0));
            packetResponse.GetPacketResponse(BlockPlace(31, 2, 6));
            packetResponse.GetPacketResponse(BlockPlace(10, -5, 6));
            packetResponse.GetPacketResponse(BlockPlace(65, 0, -9));
            
            
            Assert.AreEqual(worldBlock.GetBlock(0,0).GetBlockId(),1);
            Assert.AreEqual(worldBlock.GetBlock(2,6).GetBlockId(),31);
            Assert.AreEqual(worldBlock.GetBlock(-5,6).GetBlockId(),10);
            Assert.AreEqual(worldBlock.GetBlock(0,-9).GetBlockId(),65);
        }

        List<byte> BlockPlace(int id,int x,int y)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ByteListConverter.ToByteArray((short)1));
            bytes.AddRange(ByteListConverter.ToByteArray(id));
            bytes.AddRange(ByteListConverter.ToByteArray((short)0));
            bytes.AddRange(ByteListConverter.ToByteArray(x));
            bytes.AddRange(ByteListConverter.ToByteArray(y));
            bytes.AddRange(ByteListConverter.ToByteArray(0));
            bytes.AddRange(ByteListConverter.ToByteArray(0));

            return bytes;
        }
    }
}