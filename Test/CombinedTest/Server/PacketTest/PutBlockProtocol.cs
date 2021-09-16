using System.Collections.Generic;
using industrialization.OverallManagement.DataStore;
using NUnit.Framework;
using Server.PacketHandle;
using Server.Util;

namespace industrialization_test.CombinedTest.Server.PacketTest
{
    public class PutBlockProtocol
    {
        [Test]
        public void SimpleBlockPlaceTest()
        {
            PacketResponseCreator.GetPacketResponse(BlockPlace(1, 0, 0));
            PacketResponseCreator.GetPacketResponse(BlockPlace(31, 2, 6));
            PacketResponseCreator.GetPacketResponse(BlockPlace(10, -5, 6));
            PacketResponseCreator.GetPacketResponse(BlockPlace(65, 0, -9));
            
            
            Assert.AreEqual(WorldBlockDatastore.GetBlock(0,0).GetBlockId(),1);
            Assert.AreEqual(WorldBlockDatastore.GetBlock(2,6).GetBlockId(),31);
            Assert.AreEqual(WorldBlockDatastore.GetBlock(-5,6).GetBlockId(),10);
            Assert.AreEqual(WorldBlockDatastore.GetBlock(0,-9).GetBlockId(),65);
        }

        byte[] BlockPlace(int id,int x,int y)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ByteArrayConverter.ToByteArray((short)1));
            bytes.AddRange(ByteArrayConverter.ToByteArray(id));
            bytes.AddRange(ByteArrayConverter.ToByteArray((short)0));
            bytes.AddRange(ByteArrayConverter.ToByteArray(x));
            bytes.AddRange(ByteArrayConverter.ToByteArray(y));
            bytes.AddRange(ByteArrayConverter.ToByteArray(0));
            bytes.AddRange(ByteArrayConverter.ToByteArray(0));

            return bytes.ToArray();
        }
    }
}