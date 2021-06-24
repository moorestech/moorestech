using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Server.Util;
using industrialization.Server.Util.ByteArrayToObject;
using NUnit.Framework;

namespace industrialization.Server.Test
{
    //バイト配列と各種オブジェクトとの変換のテスト
    public class ByteObjectConvertTest
    {
        
        [Test]
        //GUIDを配列に変換して同じかテストする
        public void OneGUIDObjectToByteArrayTest()
        {
            for (int i = 0; i < 100; i++)
            {
                var guid = Guid.NewGuid();
                var ans = guid.ToByteArray();

                var exp = SendObjectToByteArray.ToByteArray(guid);

                for (int j = 0; j < ans.Length; j++)
                {
                    Assert.AreEqual(ans[j],exp[j]);
                }
            }
        }        
        [Test]
        //intを配列に変換して同じかテストする
        public void OneintObjectToByteArrayTest()
        {
            var random = new Random(1000);
            for (int i = 0; i < 100; i++)
            {

                var data = random.Next(Int32.MinValue, Int32.MaxValue);
                var ans = BitConverter.GetBytes(data);

                var exp = SendObjectToByteArray.ToByteArray(data);

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

                var exp = SendObjectToByteArray.ToByteArray(data);

                for (int j = 0; j < ans.Length; j++)
                {
                    Assert.AreEqual(ans[j],exp[j]);
                }
            }
        }

        [Test]
        //GUIDを相互変化しても問題ないかテスト
        public void OneGUIDConvertTest()
        {
            var guid = Guid.NewGuid();
            var byteData = new ByteArrayToSendObject(SendObjectToByteArray.ToByteArray(guid));

            Assert.AreEqual(guid.ToString(),byteData.MoveNextToGetGuid().ToString());
        }
        [Test]
        //intを相互変化しても問題ないかテスト
        public void OneIntConvertTest()
        {
            var Int =  new Random(1000).Next(Int32.MinValue, Int32.MaxValue);
            var byteData = new ByteArrayToSendObject(SendObjectToByteArray.ToByteArray(Int));

            Assert.AreEqual(Int,byteData.MoveNextToGetInt());
        }
        [Test]
        //shortを相互変化しても問題ないかテスト
        public void OneShortConvertTest()
        {
            var Short = (short)new Random(1000).Next(short.MinValue, short.MaxValue);
            var byteData = new ByteArrayToSendObject(SendObjectToByteArray.ToByteArray(Short));

            Assert.AreEqual(Short,byteData.MoveNextToGetShort());
        }
        
        [Test]
        //いろいろ相互変換をしても問題ないかテスト
        public void MoreConvertTest()
        {
            var random = new Random();
            var id = (short) random.Next(short.MinValue, short.MaxValue);
            var guid1 = Guid.NewGuid();
            var guid2 = Guid.NewGuid();
            var ans = new List<byte>();
            ans.AddRange(SendObjectToByteArray.ToByteArray(id));
            ans.AddRange(SendObjectToByteArray.ToByteArray(guid1));
            ans.AddRange(SendObjectToByteArray.ToByteArray(guid2));
            ans.AddRange(SendObjectToByteArray.ToByteArray(50));

            var byteData = new ByteArrayToSendObject(ans.ToArray());

            Assert.AreEqual(id,byteData.MoveNextToGetShort()) ;
            Assert.AreEqual(guid1.ToString(),byteData.MoveNextToGetGuid().ToString());
            Assert.AreEqual(guid2.ToString(),byteData.MoveNextToGetGuid().ToString());
            Assert.AreEqual(50,byteData.MoveNextToGetInt());
        }
    }
}