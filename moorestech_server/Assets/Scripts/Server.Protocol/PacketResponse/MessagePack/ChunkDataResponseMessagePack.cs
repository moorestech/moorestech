using System;
using MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.MessagePack
{
    [MessagePackObject(true)]
    public class ChunkDataResponseMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChunkDataResponseMessagePack()
        {
        }

        public ChunkDataResponseMessagePack(Vector2Int chunk, int[,] blockIds, int[,] blockDirect,
            int[,] mapTileIds)
        {
            Tag = PlayerCoordinateSendProtocol.ChunkDataTag;
            ChunkX = chunk.x;
            ChunkY = chunk.y;
            BlockIds = blockIds;
            BlockDirect = blockDirect;
            MapTileIds = mapTileIds;
        }

        public int ChunkX { get; set; }
        public int ChunkY { get; set; }

        public int[,] BlockIds { get; set; }
        public int[,] BlockDirect { get; set; }
        public int[,] MapTileIds { get; set; }
    }
}