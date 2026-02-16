using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Util;

namespace Tests.UnitTest.Server
{
    /// <summary>
    ///     <see cref="PacketBufferParser" />が正しくパースで来ているかのテスト
    /// </summary>
    public class PacketBufferParserTest
    {
        /// <summary>
        ///     オーバーフローして、ヘッダーが分離されたときに正しくパースできるかのテスト
        ///     1回目と2回目を複合して正しくパースできるかのテスト
        /// </summary>
        [Test]
        public void PacketBufferPasserNoOverflowTest()
        {
            //これは確定で5バイトになる
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });
            
            
            //すべてがぴったりと入っているパターン
            // 4Bのヘッダ + 2Bのダミー + 4Bのヘッダ + 5Bのメインデータ
            var parser = new PacketBufferParser();
            var sendBytes = new List<byte>();
            sendBytes.AddRange(BitConverter.GetBytes(2).Reverse()); //4Bのヘッダ
            sendBytes.Add(0); //2Bのダミー
            sendBytes.Add(0);
            sendBytes.AddRange(BitConverter.GetBytes(5).Reverse()); //4Bのヘッダ
            sendBytes.AddRange(testMessageBytes); //5Bのメインデータ
            
            var result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            //結果のデータの二番目が正しくパースできていることを確認する
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[1]).t);
            
            
            //ヘッダーが1バイトオーバーフローしているパターン
            //1回目のパケットを送る
            parser = new PacketBufferParser();
            sendBytes.Clear();
            var header = BitConverter.GetBytes(5).Reverse().ToList();
            sendBytes.Add(header[0]);
            sendBytes.Add(header[1]);
            sendBytes.Add(header[2]);
            //1回目パース
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual(0, result.Count); //まだ2個目はパースできない
            
            //2回目のパケットを送る
            //結果のデータの二番目が正しくパースできていることを確認する
            sendBytes.Clear();
            sendBytes.Add(header[3]); //最後のヘッダ
            sendBytes.AddRange(testMessageBytes); //5Bのメインデータ
            //2回目パース
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[0]).t);
            
            
            PacketBufferPasserNoOverflowTestSendOnly(1);
            PacketBufferPasserNoOverflowTestSendOnly(2);
            PacketBufferPasserNoOverflowTestSendOnly(3);
        }
        
        
        /// <summary>
        ///     <see cref="PacketBufferPasserNoOverflowTest" />の送信、検証だけを行う
        ///     引数に示された
        /// </summary>
        private void PacketBufferPasserNoOverflowTestSendOnly(int overflowCountByte)
        {
            //これは確定で5バイトになる
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });
            
            var parser = new PacketBufferParser();
            var sendBytes = new List<byte>();
            var header = BitConverter.GetBytes(5).Reverse().ToList();
            for (var i = 0; i < 4 - overflowCountByte; i++) sendBytes.Add(header[i]);
            //1回目パース
            var result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual(0, result.Count); //まだ2個目はパースできない
            
            //2回目のパケットを送る
            //結果のデータの二番目が正しくパースできていることを確認する
            sendBytes.Clear();
            for (var i = 4 - overflowCountByte; i < 4; i++) sendBytes.Add(header[i]);
            sendBytes.AddRange(testMessageBytes); //5Bのメインデータ
            //2回目パース
            result = parser.Parse(sendBytes.ToArray(), sendBytes.Count);
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[0]).t);
        }
    }
    
    [MessagePackObject(true)]
    public class PasserTestMessagePack
    {
        public string t;
    }
}