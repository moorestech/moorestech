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
        //GUIDを配列に変換して同じかテストする
        public void OneintObjectToByteArrayTest()
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
    }
}