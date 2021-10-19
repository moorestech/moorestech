using System.Collections.Generic;
using NUnit.Framework;
using Server.PacketHandle;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class BlockPlaceEventPacketTest
    {
        //ブロックを設置しなかった時何も返ってこないテスト
        [Test]
        public void DontBlockPlaceTest()
        {
            var response = PacketResponseCreator.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(response.Count,0);
        }
        
        //ブロックを1個設置した時に1個のブロック設置イベントが返ってくるテスト
        [Test]
        public void OneBlockPlaceEvent()
        {
            
        }

        byte[] EventRequestData(int plyaerID)
        {
            var payload = new List<byte>();
            payload.AddRange(ByteArrayConverter.ToByteArray((short)4));
            payload.AddRange(ByteArrayConverter.ToByteArray(plyaerID));
            return payload.ToArray();
        }
        
        
    }
}