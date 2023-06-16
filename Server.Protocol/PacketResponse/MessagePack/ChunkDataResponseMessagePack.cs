using System;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Protocol.Base;

namespace Server.Protocol.PacketResponse.MessagePack
{
    [MessagePackObject(keyAsPropertyName :true)]
    public class ChunkDataResponseMessagePack : ToClientProtocolMessagePackBase 
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChunkDataResponseMessagePack() { }

        public ChunkDataResponseMessagePack(Coordinate chunk, int[,] blockIds, int[,] blockDirect, int[,] mapTileIds)
        {
            ToClientTag = PlayerCoordinateSendProtocol.ChunkDataTag;
            ChunkX = chunk.X;
            ChunkY = chunk.Y;
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