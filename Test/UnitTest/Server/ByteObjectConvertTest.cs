using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.Util;
using IntId = Game.World.Interface.Util.IntId;

namespace Test.UnitTest.Server
{
    //バイト配列と各種オブジェクトとの変換のテスト
    [TestClass]
    public class ByteObjectConvertTest
    {
        [TestMethod]
        //intを相互変化しても問題ないかテスト
        public void OneIntConvertTest()
        {
            var Int = new Random(1000).Next(Int32.MaxValue, Int32.MaxValue);
            var byteData = new ByteArrayEnumerator(ToByteList.Convert(Int));

            Assert.AreEqual(Int, byteData.MoveNextToGetInt());
        }

        [TestMethod]
        //shortを相互変化しても問題ないかテスト
        public void OneShortConvertTest()
        {
            var Short = (short) new Random(1000).Next(short.MinValue, short.MaxValue);
            var byteData = new ByteArrayEnumerator(ToByteList.Convert(Short));

            Assert.AreEqual(Short, byteData.MoveNextToGetShort());
        }

        [TestMethod]
        //floatを相互変化しても問題ないかテスト
        public void OneFloatConvertTest()
        {
            var Float = (float) new Random(1000).NextDouble();
            var byteData = new ByteArrayEnumerator(ToByteList.Convert(Float));

            Assert.AreEqual(Float, byteData.MoveNextToGetFloat());
        }

        [TestMethod]
        //バイト数を指定してstringを相互変化しても問題ないかテスト
        public void OneByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteArrayEnumerator(ToByteList.Convert(String));

            Assert.AreEqual(String, byteData.MoveNextToGetString(30));
        }

        [TestMethod]
        //違うバイト数を指定してstringを相互変化して失敗させるテスト
        public void OneDifferentByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteArrayEnumerator(ToByteList.Convert(String));

            Assert.AreNotEqual(String, byteData.MoveNextToGetString(29));
        }

        [TestMethod]
        //いろいろ相互変換をしても問題ないかテスト
        public void MoreConvertTest()
        {
            var random = new Random();
            var id = (short) random.Next(short.MinValue, short.MaxValue);
            var intId1 = IntId.NewIntId();
            var intId2 = IntId.NewIntId();
            var ans = new List<byte>();
            ans.AddRange(ToByteList.Convert(id));
            ans.AddRange(ToByteList.Convert(intId1));
            ans.AddRange(ToByteList.Convert(intId2));
            ans.AddRange(ToByteList.Convert(50));

            var byteData = new ByteArrayEnumerator(ans);

            Assert.AreEqual(id, byteData.MoveNextToGetShort());
            Assert.AreEqual(intId1, byteData.MoveNextToGetInt());
            Assert.AreEqual(intId2, byteData.MoveNextToGetInt());
            Assert.AreEqual(50, byteData.MoveNextToGetInt());
        }

        [TestMethod]
        public void ConvertTest()
        {
            ConvertTest(0, 0, 0, 0, 0);
            ConvertTest(5, 0, 0, 0, 5);
            ConvertTest(500, 0, 0, 1, 244);
            ConvertTest(1546, 0, 0, 6, 10);
        }
        public void ConvertTest(int ans, byte b1, byte b2, byte b3, byte b4)
        {
            var actual = ToByteList.Convert(ans);
            var expect = new byte[4] {b1, b2, b3, b4};

            Assert.AreEqual(expect[0], actual[0]);
            Assert.AreEqual(expect[1], actual[1]);
            Assert.AreEqual(expect[2], actual[2]);
            Assert.AreEqual(expect[3], actual[3]);
        }

        [TestMethod]
        public void ByteArrayToStringTest()
        {
            var bytes = new List<byte> {0x61, 0x41, 0x72, 0x36, 0x23};

            var byteData = new ByteArrayEnumerator(bytes);
            Assert.AreEqual("aAr6#", byteData.MoveNextToGetString(5));
        }
    }
}