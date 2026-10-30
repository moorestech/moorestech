using System;
using System.Text;
using MessagePack;
using Server.Protocol;

namespace Client.Network.API
{
    // 受信パケット先頭のTag・SequenceIdだけを読み、本体の走査を省く
    // Reads only the leading Tag and SequenceId so the packet body is never walked
    public readonly struct ProtocolPacketHeader
    {
        private static readonly byte[] EventStreamTagUtf8 = Encoding.UTF8.GetBytes(EventStreamMessagePack.ProtocolTag);

        public readonly bool IsEventStream;
        public readonly int SequenceId;

        private ProtocolPacketHeader(bool isEventStream, int sequenceId)
        {
            IsEventStream = isEventStream;
            SequenceId = sequenceId;
        }

        public static ProtocolPacketHeader Read(byte[] data)
        {
            // ProtocolMessagePackBaseは配列形式で[0]=Tag, [1]=SequenceId
            // ProtocolMessagePackBase is array-encoded as [0]=Tag, [1]=SequenceId
            var reader = new MessagePackReader(new ReadOnlyMemory<byte>(data));
            reader.ReadArrayHeader();
            var isEventStream = ReadIsEventStreamTag(ref reader);
            var sequenceId = reader.ReadInt32();
            return new ProtocolPacketHeader(isEventStream, sequenceId);

            #region Internal

            static bool ReadIsEventStreamTag(ref MessagePackReader tagReader)
            {
                // Tag文字列を確保せずUTF8のまま比較する
                // Compare the tag as raw UTF8 without allocating a string
                if (tagReader.TryReadNil()) return false;
                if (tagReader.TryReadStringSpan(out var tagSpan)) return tagSpan.SequenceEqual(EventStreamTagUtf8);

                // 分割バッファ時のみ文字列化して比較する
                // Fall back to a string only when the buffer is not contiguous
                return tagReader.ReadString() == EventStreamMessagePack.ProtocolTag;
            }

            #endregion
        }
    }
}
