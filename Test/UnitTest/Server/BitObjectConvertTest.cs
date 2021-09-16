using System.Collections.Generic;
using NUnit.Framework;
using Server.Util;

namespace industrialization_test.UnitTest.Server
{
    public class BitObjectConvertTest
    {
        [Test]
        public void BitArrayEnumeratorTest()
        {
            var byteArray = new List<byte>();
            byteArray.Add(0);
            byteArray.Add(14);
            byteArray.Add(15);
            byteArray.AddRange(ByteArrayConverter.ToByteArray(10));
            byteArray.AddRange(ByteArrayConverter.ToByteArray(int.MaxValue));
            byteArray.AddRange(ByteArrayConverter.ToByteArray((short)50));
            byteArray.AddRange(ByteArrayConverter.ToByteArray(30.54f));

            var bitArray = new BitArrayEnumerator(byteArray.ToArray());
            
            Assert.AreEqual(0,bitArray.MoveNextToByte());
            Assert.AreEqual(14,bitArray.MoveNextToByte());
            
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            
            Assert.AreEqual(10,bitArray.MoveNextToInt());
            Assert.AreEqual(int.MaxValue,bitArray.MoveNextToInt());
            Assert.AreEqual((short)50,bitArray.MoveNextToShort());
            Assert.AreEqual(30.54f,bitArray.MoveNextToFloat());
        }
        
        
        [Test]
        public void BitArrayToBitArrayEnumeratorTest()
        {
            var boolArray = new List<bool>();
            boolArray.AddRange(ByteArrayToBitArray.Convert((byte)0));
            boolArray.AddRange(ByteArrayToBitArray.Convert((byte)14));
            boolArray.Add(true);
            boolArray.Add(false);
            boolArray.AddRange(ByteArrayToBitArray.Convert((byte)60));
            boolArray.Add(true);
            boolArray.Add(false);
            boolArray.Add(true);
            boolArray.AddRange(ByteArrayToBitArray.Convert(5426));

            var bytes = BitArrayToByteArray.Convert(boolArray.ToArray());
            var bitArray = new BitArrayEnumerator(bytes);
            
            Assert.AreEqual(0,bitArray.MoveNextToByte());
            Assert.AreEqual(14,bitArray.MoveNextToByte());
            
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            
            Assert.AreEqual(60,bitArray.MoveNextToByte());
            
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            Assert.AreEqual(false,bitArray.MoveNextToBit());
            Assert.AreEqual(true,bitArray.MoveNextToBit());
            
            Assert.AreEqual(5426,bitArray.MoveNextToInt());
            
        }
    }
}