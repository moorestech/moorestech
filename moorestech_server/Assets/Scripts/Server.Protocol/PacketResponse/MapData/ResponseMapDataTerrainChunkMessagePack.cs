using System;
using MessagePack;

namespace Server.Protocol.PacketResponse.MapData
{
    // 論理ストリームをChunkByteSizeで切り出した1断片。Payloadは非圧縮断片のGZip圧縮結果
    // One slice of the logical stream cut at ChunkByteSize; Payload is the GZip-compressed uncompressed slice
    [MessagePackObject]
    public class ResponseMapDataTerrainChunkMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public int ChunkIndex { get; set; }
        [Key(3)] public byte[] Payload { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseMapDataTerrainChunkMessagePack() { }

        public ResponseMapDataTerrainChunkMessagePack(int chunkIndex, byte[] payload)
        {
            Tag = GetMapDataProtocol.ProtocolTag;
            ChunkIndex = chunkIndex;
            Payload = payload;
        }
    }
}
