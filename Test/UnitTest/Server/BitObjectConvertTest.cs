using System.Collections.Generic;
using NUnit.Framework;
using Server.Util;

namespace Test.UnitTest.Server
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
            byteArray.AddRange(ByteListConverter.ToByteArray(10));
            byteArray.AddRange(ByteListConverter.ToByteArray(int.MaxValue));
            byteArray.AddRange(ByteListConverter.ToByteArray((short) 50));
            byteArray.AddRange(ByteListConverter.ToByteArray(30.54f));

            var bitArray = new BitListEnumerator(byteArray);

            Assert.AreEqual(0, bitArray.MoveNextToByte());
            Assert.AreEqual(14, bitArray.MoveNextToByte());

            Assert.AreEqual(false, bitArray.MoveNextToBit());
            Assert.AreEqual(false, bitArray.MoveNextToBit());
            Assert.AreEqual(false, bitArray.MoveNextToBit());
            Assert.AreEqual(false, bitArray.MoveNextToBit());
            Assert.AreEqual(true, bitArray.MoveNextToBit());
            Assert.AreEqual(true, bitArray.MoveNextToBit());
            Assert.AreEqual(true, bitArray.MoveNextToBit());
            Assert.AreEqual(true, bitArray.MoveNextToBit());

            Assert.AreEqual(10, bitArray.MoveNextToInt());
            Assert.AreEqual(int.MaxValue, bitArray.MoveNextToInt());
            Assert.AreEqual((short) 50, bitArray.MoveNextToShort());
            Assert.AreEqual(30.54f, bitArray.MoveNextToFloat());
        }


        [Test]
        public void BitArrayToBitArrayEnumeratorTest()
        {
            var boolArray = new List<bool>();
            boolArray.AddRange(ByteListToBitList.Convert((byte) 0));
            boolArray.AddRange(ByteListToBitList.Convert((byte) 14));
            boolArray.Add(true);
            boolArray.Add(false);
            boolArray.AddRange(ByteListToBitList.Convert((byte) 60));
            boolArray.Add(true);
            boolArray.Add(false);
            boolArray.Add(true);
            boolArray.AddRange(ByteListToBitList.Convert(5426));
            boolArray.AddRange(ByteListToBitList.Convert((byte) 8));

            var bytes = BitListToByteList.Convert(boolArray);
            var bitArray = new BitListEnumerator(bytes);

            Assert.AreEqual(0, bitArray.MoveNextToByte());
            Assert.AreEqual(14, bitArray.MoveNextToByte());

            Assert.AreEqual(true, bitArray.MoveNextToBit());
            Assert.AreEqual(false, bitArray.MoveNextToBit());

            Assert.AreEqual(60, bitArray.MoveNextToByte());

            Assert.AreEqual(true, bitArray.MoveNextToBit());
            Assert.AreEqual(false, bitArray.MoveNextToBit());
            Assert.AreEqual(true, bitArray.MoveNextToBit());

            Assert.AreEqual(5426, bitArray.MoveNextToInt());
            Assert.AreEqual(8, bitArray.MoveNextToByte());
        }
    }
}