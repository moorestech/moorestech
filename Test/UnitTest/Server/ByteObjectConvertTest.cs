using System;
using System.Collections.Generic;
using Core;
using Core.Util;
using industrialization.Server.Util;
using NUnit.Framework;

namespace industrialization_test.UnitTest.Server
{
    //バイト配列と各種オブジェクトとの変換のテスト
    public class ByteObjectConvertTest
    {      
        [Test]
        //intを配列に変換して同じかテストする
        public void OneintObjectToByteArrayTest()
        {
            var random = new Random(1000);
            for (int i = 0; i < 100; i++)
            {

                var data = random.Next(Int32.MaxValue, Int32.MaxValue);
                var ans = BitConverter.GetBytes(data);

                var exp = ByteArrayConverter.ToByteArray(data);

                for (int j = 0; j < ans.Length; j++)
                {
                    Assert.AreEqual(ans[j],exp[j]);
                }
            }
        }        
        [Test]
        //shortを配列に変換して同じかテストする
        public void OneshortObjectToByteArrayTest()
        {
            var random = new Random(1000);
            for (int i = 0; i < 100; i++)
            {

                var data = (short)random.Next(short.MinValue,short.MaxValue);
                var ans = BitConverter.GetBytes(data);

                var exp = ByteArrayConverter.ToByteArray(data);

                for (int j = 0; j < ans.Length; j++)
                {
                    Assert.AreEqual(ans[j],exp[j]);
                }
            }
        }
        [Test]
        //intを相互変化しても問題ないかテスト
        public void OneIntConvertTest()
        {
            var Int =  new Random(1000).Next(Int32.MaxValue, Int32.MaxValue);
            var byteData = new ByteArrayEnumerator(ByteArrayConverter.ToByteArray(Int));

            Assert.AreEqual(Int,byteData.MoveNextToGetInt());
        }
        [Test]
        //shortを相互変化しても問題ないかテスト
        public void OneShortConvertTest()
        {
            var Short = (short)new Random(1000).Next(short.MinValue, short.MaxValue);
            var byteData = new ByteArrayEnumerator(ByteArrayConverter.ToByteArray(Short));

            Assert.AreEqual(Short,byteData.MoveNextToGetShort());
        }
        [Test]
        //floatを相互変化しても問題ないかテスト
        public void OneFloatConvertTest()
        {
            var Float = (float)new Random(1000).NextDouble();
            var byteData = new ByteArrayEnumerator(ByteArrayConverter.ToByteArray(Float));

            Assert.AreEqual(Float,byteData.MoveNextToGetFloat());
        }
        [Test]
        //バイト数を指定してstringを相互変化しても問題ないかテスト
        public void OneByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteArrayEnumerator(ByteArrayConverter.ToByteArray(String));

            Assert.AreEqual(String,byteData.MoveNextToGetString(30));
        }
        [Test]
        //違うバイト数を指定してstringを相互変化して失敗させるテスト
        public void OneDifferentByteNumStringConvertTest()
        {
            var String = "変換test012()あいうaＢC:";
            var byteData = new ByteArrayEnumerator(ByteArrayConverter.ToByteArray(String));

            Assert.AreNotEqual(String,byteData.MoveNextToGetString(29));
        }
        
        [Test]
        //いろいろ相互変換をしても問題ないかテスト
        public void MoreConvertTest()
        {
            var random = new Random();
            var id = (short) random.Next(short.MinValue, short.MaxValue);
            var intId1 = IntId.NewIntId();
            var intId2 = IntId.NewIntId();
            var ans = new List<byte>();
            ans.AddRange(ByteArrayConverter.ToByteArray(id));
            ans.AddRange(ByteArrayConverter.ToByteArray(intId1));
            ans.AddRange(ByteArrayConverter.ToByteArray(intId2));
            ans.AddRange(ByteArrayConverter.ToByteArray(50));

            var byteData = new ByteArrayEnumerator(ans.ToArray());

            Assert.AreEqual(id,byteData.MoveNextToGetShort()) ;
            Assert.AreEqual(intId1,byteData.MoveNextToGetInt());
            Assert.AreEqual(intId2,byteData.MoveNextToGetInt());
            Assert.AreEqual(50,byteData.MoveNextToGetInt());
        }
    }
}