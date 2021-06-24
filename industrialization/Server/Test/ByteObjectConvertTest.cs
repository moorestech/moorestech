using System;
using System.Linq;
using industrialization.Server.Util;
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
            var random = new Random();
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
            var random = new Random();
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
    }
}