#if NET6_0
using System;
using System.Collections.Generic;
using Game.World.Interface.Util;
using NUnit.Framework;
using Server.Util;


namespace Test.UnitTest.Server
{
    //バイト配列と各種オブジェクトとの変換のテスト
    public class ByteObjectConvertTest
    {
        [Test]
        //intを相互変化しても問題ないかテスト
        public void OneIntConvertTest()
        {
            var Int = new Random(1000).Next(Int32.MaxValue, Int32.MaxValue);
            var byteData = new ByteListEnumerator(ToByteList.Convert(Int));

            Assert.AreEqual(Int, byteData.MoveNextToGetInt());
        }

        [Test]
        //shortを相互変化しても問題ないかテスト
        public void OneShortConvertTest()
        {
            var Short = (short) new Random(1000).Next(short.MinValue, short.MaxValue);
            var byteData = new ByteListEnumerator(ToByteList.Convert(Short));

            Assert.AreEqual(Short, byteData.MoveNextToGetShort());
        }

        [Test]
        //floatを相互変化しても問題ないかテスト
        public void OneFloatConvertTest()
        {
            var Float = (float) new Random(1000).NextDouble();
            var byteData = new ByteListEnumerator(ToByteList.Convert(Float));

            Assert.AreEqual(Float, byteData.MoveNextToGetFloat());
        }

        [Test]
        //バイト数を指定してstringを相互変化しても問題ないかテスト
        public void OneByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteListEnumerator(ToByteList.Convert(String));

            Assert.AreEqual(String, byteData.MoveNextToGetString(30));
        }

        [Test]
        //違うバイト数を指定してstringを相互変化して失敗させるテスト
        public void OneDifferentByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteListEnumerator(ToByteList.Convert(String));

            Assert.AreNotEqual(String, byteData.MoveNextToGetString(29));
        }

        [Test]
        //いろいろ相互変換をしても問題ないかテスト
        public void MoreConvertTest()
        {
            var random = new Random();
            var id = (short) random.Next(short.MinValue, short.MaxValue);
            var entityId1 = CreateBlockEntityId.Create();
            var entityId2 = CreateBlockEntityId.Create();
            var ans = new List<byte>();
            ans.AddRange(ToByteList.Convert(id));
            ans.AddRange(ToByteList.Convert(entityId1));
            ans.AddRange(ToByteList.Convert(entityId2));
            ans.AddRange(ToByteList.Convert(50));

            var byteData = new ByteListEnumerator(ans);

            Assert.AreEqual(id, byteData.MoveNextToGetShort());
            Assert.AreEqual(entityId1, byteData.MoveNextToGetInt());
            Assert.AreEqual(entityId2, byteData.MoveNextToGetInt());
            Assert.AreEqual(50, byteData.MoveNextToGetInt());
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(5, 0, 0, 0, 5)]
        [TestCase(500, 0, 0, 1, 244)]
        [TestCase(1546, 0, 0, 6, 10)]
        public void ConvertTest(int ans, byte b1, byte b2, byte b3, byte b4)
        {
            var actual = ToByteList.Convert(ans);
            var expect = new byte[4] {b1, b2, b3, b4};

            Assert.AreEqual(expect[0], actual[0]);
            Assert.AreEqual(expect[1], actual[1]);
            Assert.AreEqual(expect[2], actual[2]);
            Assert.AreEqual(expect[3], actual[3]);
        }

        [Test]
        public void ByteArrayToStringTest()
        {
            var bytes = new List<byte> {0x61, 0x41, 0x72, 0x36, 0x23};

            var byteData = new ByteListEnumerator(bytes);
            Assert.AreEqual("aAr6#", byteData.MoveNextToGetString(5));
        }
    }
}
#endif