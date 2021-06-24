using System;
using System.Collections.Generic;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.PacketResponse;
using industrialization.Server.PacketResponse.ProtocolImplementation;
using industrialization.Server.Util;
using NUnit.Framework;

namespace industrialization.Server.Test
{
    //クライアントからのバイト配列から正しいレスポンスが返るかのテスト
    public class PacketResponseFactoryTest
    {
        //なにも設定してないときに空の配列を取得する
        [Test]
        public void Building1ChunkInstalledWhenCorrectlyDataAcquirableIsTest()
        {
            var send = new List<byte>();
            send.AddRange(ByteArrayConverter.ToByteArray((short)2));
            send.AddRange(ByteArrayConverter.ToByteArray((short)0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            send.AddRange(ByteArrayConverter.ToByteArray(0));
            var response = PacketResponseFactory.GetPacketResponse(send.ToArray());
            var ansResponse  = new List<byte>();
            ansResponse.AddRange(ByteArrayConverter.ToByteArray((short)1));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray((short)0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));
            ansResponse.AddRange(ByteArrayConverter.ToByteArray(0));


            for (int i = 0; i < response.Length; i++)
            {
                Console.WriteLine(i);
                Assert.AreEqual(ansResponse[i],response[i]);
            }
        }
    }
}