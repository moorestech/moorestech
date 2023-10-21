#if NET6_0
using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Util;

namespace Test.UnitTest.Server
{
    /// <summary>
    ///     <see cref="PacketBufferParser" />
    /// </summary>
    public class PacketBufferParserTest
    {

        ///     
        ///     12

        [Test]
        public void PacketBufferPasserNoOverflowTest()
        {
            //5
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });


            
            // 4B + 2B + 4B + 5B
            var parser = new PacketBufferParser();
            var sendBytes = new List<byte>();
            sendBytes.AddRange(BitConverter.GetBytes(2).Reverse()); //4B
            sendBytes.Add(0); //2B
            sendBytes.Add(0);
            sendBytes.AddRange(BitConverter.GetBytes(5).Reverse()); //4B
            sendBytes.AddRange(testMessageBytes); //5B

            var result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[1].ToArray()).t);


            //1
            //1
            parser = new PacketBufferParser();
            sendBytes.Clear();
            var header = BitConverter.GetBytes(5).Reverse().ToList();
            sendBytes.Add(header[0]);
            sendBytes.Add(header[1]);
            sendBytes.Add(header[2]);
            //1
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual(0, result.Count); //2

            //2
            
            sendBytes.Clear();
            sendBytes.Add(header[3]); 
            sendBytes.AddRange(testMessageBytes); //5B
            //2
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[0].ToArray()).t);


            PacketBufferPasserNoOverflowTestSendOnly(1);
            PacketBufferPasserNoOverflowTestSendOnly(2);
            PacketBufferPasserNoOverflowTestSendOnly(3);
        }



        ///     <see cref="PacketBufferPasserNoOverflowTest" />
        ///     

        private void PacketBufferPasserNoOverflowTestSendOnly(int overflowCountByte)
        {
            //5
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });

            var parser = new PacketBufferParser();
            var sendBytes = new List<byte>();
            var header = BitConverter.GetBytes(5).Reverse().ToList();
            for (var i = 0; i < 4 - overflowCountByte; i++) sendBytes.Add(header[i]);
            //1
            var result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual(0, result.Count); //2

            //2
            
            sendBytes.Clear();
            for (var i = 4 - overflowCountByte; i < 4; i++) sendBytes.Add(header[i]);
            sendBytes.AddRange(testMessageBytes); //5B
            //2
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[0].ToArray()).t);
        }
    }

    [MessagePackObject(true)]
    public class PasserTestMessagePack
    {
        public string t;
    }
}
#endif