using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Util;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// PacketBufferParserのオーバーフローテスト
    /// 実際のバッファサイズと受信バイト数が異なる場合の動作を検証
    /// </summary>
    public class PacketBufferParserOverflowTest
    {
        /// <summary>
        /// バッファサイズが受信バイト数より大きい場合、
        /// 正しく受信バイト数のみを使用してパースできることを確認する
        /// </summary>
        [Test]
        public void ParseWithLargerBufferThanActualData()
        {
            // テストデータを作成（5バイト固定）
            // Create test data (5 bytes fixed)
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });
            Assert.AreEqual(5, testMessageBytes.Length, "テストデータは5バイトであるべき");

            // ヘッダ(4B) + ペイロード(5B) = 9バイトのパケットを作成
            // Create a 9-byte packet: header(4B) + payload(5B)
            var packetData = new List<byte>();
            packetData.AddRange(BitConverter.GetBytes(5).Reverse()); // 4バイトのヘッダ（ビッグエンディアン）
            packetData.AddRange(testMessageBytes); // 5バイトのペイロード

            // 4096バイトのバッファを使用（実際のサーバー通信と同じ）
            // Use a 4096-byte buffer (same as actual server communication)
            var buffer = new byte[4096];
            packetData.ToArray().CopyTo(buffer, 0);

            // バッファの残りにガベージデータを入れる
            // Fill the rest of the buffer with garbage data
            for (var i = packetData.Count; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'n'; // 'n' = 110 は MessagePackで正しくないコード
            }

            var parser = new PacketBufferParser();
            var result = parser.Parse(buffer, packetData.Count); // 実際の受信バイト数は9

            Assert.AreEqual(1, result.Count, "1つのパケットがパースされるべき");
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result[0].ToArray()).t);
        }

        /// <summary>
        /// ヘッダが分割されて届いた場合、バッファサイズより小さい実際のデータで正しくパースできるか
        /// </summary>
        [Test]
        public void ParseWithSplitHeaderAndLargerBuffer()
        {
            var testMessageBytes = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "t" });
            var header = BitConverter.GetBytes(5).Reverse().ToArray();

            var parser = new PacketBufferParser();

            // 1回目: ヘッダの最初の3バイトのみ送信
            // First: send only first 3 bytes of header
            var buffer1 = new byte[4096];
            buffer1[0] = header[0];
            buffer1[1] = header[1];
            buffer1[2] = header[2];
            // 残りはガベージ
            for (var i = 3; i < buffer1.Length; i++) buffer1[i] = (byte)'X';

            var result1 = parser.Parse(buffer1, 3);
            Assert.AreEqual(0, result1.Count, "まだパケットは完成していないはず");

            // 2回目: ヘッダの最後の1バイト + ペイロード
            // Second: last 1 byte of header + payload
            var buffer2 = new byte[4096];
            buffer2[0] = header[3];
            testMessageBytes.CopyTo(buffer2, 1);
            // 残りはガベージ
            for (var i = 6; i < buffer2.Length; i++) buffer2[i] = (byte)'Y';

            var result2 = parser.Parse(buffer2, 6);
            Assert.AreEqual(1, result2.Count, "1つのパケットがパースされるべき");
            Assert.AreEqual("t", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result2[0].ToArray()).t);
        }

        /// <summary>
        /// ペイロードが分割されて届いた場合のテスト
        /// </summary>
        [Test]
        public void ParseWithSplitPayloadAndLargerBuffer()
        {
            // 大きめのテストデータを作成
            // Create larger test data
            var testData = new LargerTestMessagePack { Data = new string('A', 100) };
            var testMessageBytes = MessagePackSerializer.Serialize(testData);
            var payloadLength = testMessageBytes.Length;

            var header = BitConverter.GetBytes(payloadLength).Reverse().ToArray();

            var parser = new PacketBufferParser();

            // 1回目: ヘッダ(4B) + ペイロードの一部(10B)
            // First: header(4B) + partial payload(10B)
            var buffer1 = new byte[4096];
            header.CopyTo(buffer1, 0);
            Array.Copy(testMessageBytes, 0, buffer1, 4, 10);
            // 残りはガベージ
            for (var i = 14; i < buffer1.Length; i++) buffer1[i] = (byte)'n';

            var result1 = parser.Parse(buffer1, 14);
            Assert.AreEqual(0, result1.Count, "まだパケットは完成していないはず");

            // 2回目: ペイロードの残り
            // Second: rest of payload
            var buffer2 = new byte[4096];
            var remainingLength = payloadLength - 10;
            Array.Copy(testMessageBytes, 10, buffer2, 0, remainingLength);
            // 残りはガベージ
            for (var i = remainingLength; i < buffer2.Length; i++) buffer2[i] = (byte)'n';

            var result2 = parser.Parse(buffer2, remainingLength);
            Assert.AreEqual(1, result2.Count, "1つのパケットがパースされるべき");

            var deserializedData = MessagePackSerializer.Deserialize<LargerTestMessagePack>(result2[0].ToArray());
            Assert.AreEqual(testData.Data, deserializedData.Data);
        }

        /// <summary>
        /// 複数パケットが連続で届き、最後のパケットが分割される場合のテスト
        /// </summary>
        [Test]
        public void ParseWithMultiplePacketsAndSplitLastPacket()
        {
            var testMessage1 = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "1" });
            var testMessage2 = MessagePackSerializer.Serialize(new PasserTestMessagePack { t = "2" });
            var largeData = new LargerTestMessagePack { Data = new string('B', 200) };
            var testMessage3 = MessagePackSerializer.Serialize(largeData);

            var header1 = BitConverter.GetBytes(testMessage1.Length).Reverse().ToArray();
            var header2 = BitConverter.GetBytes(testMessage2.Length).Reverse().ToArray();
            var header3 = BitConverter.GetBytes(testMessage3.Length).Reverse().ToArray();

            var parser = new PacketBufferParser();

            // 1回目: パケット1(完全) + パケット2(完全) + パケット3のヘッダとペイロードの一部
            // First: packet1(complete) + packet2(complete) + packet3 header and partial payload
            var buffer1 = new byte[4096];
            var offset = 0;

            header1.CopyTo(buffer1, offset);
            offset += 4;
            testMessage1.CopyTo(buffer1, offset);
            offset += testMessage1.Length;

            header2.CopyTo(buffer1, offset);
            offset += 4;
            testMessage2.CopyTo(buffer1, offset);
            offset += testMessage2.Length;

            header3.CopyTo(buffer1, offset);
            offset += 4;
            var partialPayload3 = 50;
            Array.Copy(testMessage3, 0, buffer1, offset, partialPayload3);
            offset += partialPayload3;

            var actualLength1 = offset;
            // 残りはガベージ
            for (var i = actualLength1; i < buffer1.Length; i++) buffer1[i] = (byte)'n';

            var result1 = parser.Parse(buffer1, actualLength1);
            Assert.AreEqual(2, result1.Count, "2つのパケットがパースされるべき");
            Assert.AreEqual("1", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result1[0].ToArray()).t);
            Assert.AreEqual("2", MessagePackSerializer.Deserialize<PasserTestMessagePack>(result1[1].ToArray()).t);

            // 2回目: パケット3の残り
            // Second: rest of packet3
            var buffer2 = new byte[4096];
            var remainingLength = testMessage3.Length - partialPayload3;
            Array.Copy(testMessage3, partialPayload3, buffer2, 0, remainingLength);
            // 残りはガベージ
            for (var i = remainingLength; i < buffer2.Length; i++) buffer2[i] = (byte)'n';

            var result2 = parser.Parse(buffer2, remainingLength);
            Assert.AreEqual(1, result2.Count, "1つのパケットがパースされるべき");

            var deserializedData = MessagePackSerializer.Deserialize<LargerTestMessagePack>(result2[0].ToArray());
            Assert.AreEqual(largeData.Data, deserializedData.Data);
        }

        /// <summary>
        /// ProtocolMessagePackBaseを使用した実際のプロトコルに近いテスト
        /// </summary>
        [Test]
        public void ParseWithProtocolMessagePackBase()
        {
            var testProtocol = new TestProtocolMessagePack
            {
                Tag = "test-protocol",
                SequenceId = 12345,
                ExtraData = "Hello World"
            };
            var testMessageBytes = MessagePackSerializer.Serialize(testProtocol);

            var header = BitConverter.GetBytes(testMessageBytes.Length).Reverse().ToArray();

            var parser = new PacketBufferParser();

            // バッファにガベージデータを含めてテスト
            // Test with garbage data in buffer
            var buffer = new byte[4096];
            header.CopyTo(buffer, 0);
            testMessageBytes.CopyTo(buffer, 4);
            var actualLength = 4 + testMessageBytes.Length;

            // 残りはガベージ ('n' = 110)
            for (var i = actualLength; i < buffer.Length; i++) buffer[i] = (byte)'n';

            var result = parser.Parse(buffer, actualLength);

            Assert.AreEqual(1, result.Count, "1つのパケットがパースされるべき");

            var deserialized = MessagePackSerializer.Deserialize<TestProtocolMessagePack>(result[0].ToArray());
            Assert.AreEqual(testProtocol.Tag, deserialized.Tag);
            Assert.AreEqual(testProtocol.SequenceId, deserialized.SequenceId);
            Assert.AreEqual(testProtocol.ExtraData, deserialized.ExtraData);
        }
    }

    [MessagePackObject(true)]
    public class LargerTestMessagePack
    {
        public string Data;
    }

    [MessagePackObject]
    public class TestProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)]
        public string ExtraData { get; set; }
    }
}
