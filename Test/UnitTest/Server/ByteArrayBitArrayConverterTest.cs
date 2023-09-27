using System.Collections.Generic;
using NUnit.Framework;
using Server.Util;

#if NET6_0
namespace Test.UnitTest.Server
{
    public class ByteArrayBitArrayConverterTest
    {
        [Test]
        public void ByteNumberToByteArrayTest()
        {
            var bitArray = new List<bool>
            {
                false,
                false,
                false,
                false,
                true,
                true,
                true,
                true
            };
            var enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(15, enumerator.MoveNextToGetByte());

            bitArray.Clear();
            bitArray = new List<bool>
            {
                true,
                true,
                true,
                true,
                false,
                false,
                false,
                false
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(240, enumerator.MoveNextToGetByte());

            bitArray.Clear();
            bitArray = new List<bool>
            {
                true,
                false,
                true,
                false,
                false,
                false,
                true,
                true
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(163, enumerator.MoveNextToGetByte());

            bitArray.Clear();
            bitArray = new List<bool>
            {
                false, false, false, false, false, false, false, false,
                false,
                false,
                false,
                false,
                true,
                true,
                true,
                true,
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(15, enumerator.MoveNextToGetShort());

            bitArray.Clear();
            bitArray = new List<bool>
            {
                false, false, false, false, false, false, false, false,
                true,
                true,
                true,
                true,
                false,
                false,
                false,
                false,
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(240, enumerator.MoveNextToGetShort());

            bitArray.Clear();
            bitArray = new List<bool>
            {
                false, false, false, false, false, false, false, false,
                true,
                false,
                true,
                false,
                false,
                false,
                true,
                true,
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(163, enumerator.MoveNextToGetShort());


            bitArray.Clear();
            bitArray = new List<bool>
            {
                false,
                false,
                false,
                false,
                true,
                true,
                true,
                true,
            };
            enumerator = new ByteListEnumerator(BitListToByteList.Convert(bitArray));
            Assert.AreEqual(15, enumerator.MoveNextToGetByte());
        }

        [Test]
        public void ByteArrayToBitArrayTest()
        {
            var byteList = new List<byte> {0, 10, 100};
            byteList.AddRange(ToByteList.Convert(1234546));
            byteList.AddRange(ToByteList.Convert(506.35f));
            var bitEnum = new BitListEnumerator(BitListToByteList.Convert(ToBitList.Convert(byteList)));
            Assert.AreEqual(bitEnum.MoveNextToByte(), 0);
            Assert.AreEqual(bitEnum.MoveNextToByte(), 10);
            Assert.AreEqual(bitEnum.MoveNextToByte(), 100);
            Assert.AreEqual(bitEnum.MoveNextToInt(), 1234546);
            Assert.AreEqual(bitEnum.MoveNextToFloat(), 506.35f);
        }
    }
}
#endif