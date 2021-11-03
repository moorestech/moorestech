using System;
using System.Collections.Generic;
using NUnit.Framework;
using Server.PacketHandle.PacketResponse.Player;
using Server.Util;
using World.Util;

namespace Test.UnitTest.Server.Player
{
    public class BlockToPayloadTest
    {
        [Test]
        public void NumberConvertTest()
        {
            var blocks = new int[,]
            {
                { 0,0,5},
                { 0,-1,6},
                { -1,-1,-1}
            };
            
            var ans = new List<byte>();
            ans.AddRange(ByteListConverter.ToByteArray((byte)1));
            ans.AddRange(ByteListConverter.ToByteArray(0));
            ans.AddRange(ByteListConverter.ToByteArray(0));
            var ansBool = new List<bool>();
            ansBool.Add(true);
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.AddRange(ByteListToBitList.Convert((byte)0));
            
            ansBool.Add(true);
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.AddRange(ByteListToBitList.Convert((byte)0));
            
            ansBool.Add(true);
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.AddRange(ByteListToBitList.Convert((byte)5));
            
            ansBool.Add(true);
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.AddRange(ByteListToBitList.Convert((byte)0));
            
            ansBool.Add(false);
            
            ansBool.Add(true);
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.AddRange(ByteListToBitList.Convert((byte)6));
            
            ansBool.Add(false);
            ansBool.Add(false);
            ansBool.Add(false);
            
            ans.AddRange(BitListToByteList.Convert(ansBool));

            var result = ChunkBlockToPayload.Convert(blocks, CoordinateCreator.New(0, 0));

            for (int i = 0; i < ans.Count; i++)
            {
                Console.WriteLine(i);
                Assert.AreEqual(ans[i],result[i]);
            }
        }
    }
}